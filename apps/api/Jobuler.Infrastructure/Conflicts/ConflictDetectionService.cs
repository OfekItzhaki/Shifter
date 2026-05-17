using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jobuler.Application.Conflicts;
using Jobuler.Application.Notifications;
using Jobuler.Domain.Conflicts;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Conflicts;

/// <summary>
/// Implements cross-group conflict detection for both publish and login triggers.
/// Uses ConflictDetectionDbContext (no RLS) for cross-space queries.
/// Never throws — all errors are logged and swallowed (fire-and-forget context).
/// </summary>
public class ConflictDetectionService : IConflictDetectionService
{
    private readonly ConflictDetectionDbContext _db;
    private readonly IPushNotificationSender _pushSender;
    private readonly ILogger<ConflictDetectionService> _logger;

    private const string EventType = "schedule.cross_group_conflict";

    public ConflictDetectionService(
        ConflictDetectionDbContext db,
        IPushNotificationSender pushSender,
        ILogger<ConflictDetectionService> logger)
    {
        _db = db;
        _pushSender = pushSender;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task DetectOnPublishAsync(Guid spaceId, Guid versionId, CancellationToken ct = default)
    {
        try
        {
            // Step 1: Find all persons with assignments in the published version
            var personIds = await _db.Assignments
                .Where(a => a.ScheduleVersionId == versionId)
                .Select(a => a.PersonId)
                .Distinct()
                .ToListAsync(ct);

            if (personIds.Count == 0) return;

            // Step 2: Load person records with LinkedUserId (skip those without — Req 1.5)
            var persons = await _db.People
                .Where(p => personIds.Contains(p.Id) && p.LinkedUserId != null)
                .ToListAsync(ct);

            if (persons.Count == 0) return;

            // Step 3: Get unique LinkedUserIds to process
            var linkedUserIds = persons
                .Select(p => p.LinkedUserId!.Value)
                .Distinct()
                .ToList();

            // Step 4: For each linked user, detect conflicts across all their person records
            foreach (var userId in linkedUserIds)
            {
                await DetectForUserAsync(userId, futureOnly: false, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error during conflict detection on publish for space {SpaceId}, version {VersionId}",
                spaceId, versionId);
        }
    }

    /// <inheritdoc />
    public async Task DetectOnLoginAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            // Find all Person records linked to this user (Req 2.1)
            var personRecords = await _db.People
                .Where(p => p.LinkedUserId == userId)
                .ToListAsync(ct);

            // Skip if no Person records found (Req 2.2)
            if (personRecords.Count == 0) return;

            await DetectForUserAsync(userId, futureOnly: true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error during conflict detection on login for user {UserId}", userId);
        }
    }

    /// <summary>
    /// Core detection logic for a single user across all their person records.
    /// </summary>
    private async Task DetectForUserAsync(Guid userId, bool futureOnly, CancellationToken ct)
    {
        // Step 1: Find all Person records for this user across all spaces
        var personRecords = await _db.People
            .Where(p => p.LinkedUserId == userId)
            .ToListAsync(ct);

        if (personRecords.Count == 0) return;

        var personIds = personRecords.Select(p => p.Id).ToList();

        // Step 2: Load all assignments from published versions for these persons
        var query = _db.Assignments
            .Where(a => personIds.Contains(a.PersonId))
            .Join(
                _db.ScheduleVersions.Where(sv => sv.Status == ScheduleVersionStatus.Published),
                a => a.ScheduleVersionId,
                sv => sv.Id,
                (a, sv) => a);

        var assignments = await query.ToListAsync(ct);

        if (assignments.Count == 0) return;

        // Step 3: Load task slots for time ranges
        var taskSlotIds = assignments.Select(a => a.TaskSlotId).Distinct().ToList();
        var taskSlots = await _db.TaskSlots
            .Where(ts => taskSlotIds.Contains(ts.Id))
            .ToDictionaryAsync(ts => ts.Id, ct);

        // Filter to future assignments only for login trigger (Req 2.3)
        if (futureOnly)
        {
            var now = DateTime.UtcNow;
            assignments = assignments
                .Where(a => taskSlots.ContainsKey(a.TaskSlotId) && taskSlots[a.TaskSlotId].EndsAt > now)
                .ToList();

            if (assignments.Count == 0) return;
        }

        // Step 4: Load GroupTasks for group linkage
        var taskTypeIds = taskSlots.Values.Select(ts => ts.TaskTypeId).Distinct().ToList();
        var groupTasks = await _db.GroupTasks
            .Where(gt => taskTypeIds.Contains(gt.Id))
            .ToDictionaryAsync(gt => gt.Id, ct);

        // Step 5: Load groups for MinRestBetweenShiftsHours and Name
        var groupIds = groupTasks.Values.Select(gt => gt.GroupId).Distinct().ToList();
        var groups = await _db.Groups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, ct);

        // Step 6: Build FlatAssignment list
        var flatAssignments = new List<FlatAssignment>();
        foreach (var assignment in assignments)
        {
            if (!taskSlots.TryGetValue(assignment.TaskSlotId, out var slot)) continue;
            if (!groupTasks.TryGetValue(slot.TaskTypeId, out var groupTask)) continue;
            if (!groups.TryGetValue(groupTask.GroupId, out var group)) continue;

            flatAssignments.Add(new FlatAssignment(
                AssignmentId: assignment.Id,
                GroupId: group.Id,
                GroupName: group.Name,
                TaskSlotId: slot.Id,
                StartsAt: slot.StartsAt,
                EndsAt: slot.EndsAt));
        }

        if (flatAssignments.Count < 2) return;

        // Step 7: Run conflict detection
        int GetMinRestHours(Guid groupA, Guid groupB)
        {
            var restA = groups.TryGetValue(groupA, out var gA) ? gA.MinRestBetweenShiftsHours : 0;
            var restB = groups.TryGetValue(groupB, out var gB) ? gB.MinRestBetweenShiftsHours : 0;
            return Math.Max(restA, restB);
        }

        var result = ConflictDetector.Detect(flatAssignments, GetMinRestHours);

        if (result.Conflicts.Count == 0) return;

        // Step 8: Compute deduplication fingerprint
        var dedupHash = ComputeDeduplicationHash(result.Conflicts);

        // Step 9: Create notifications per space (Req 6.5 — cross-space privacy)
        var spaceIds = personRecords.Select(p => p.SpaceId).Distinct().ToList();
        var spaces = await _db.Spaces
            .Where(s => spaceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        // Determine which groups belong to which space
        var groupSpaceMap = groups.ToDictionary(g => g.Key, g => g.Value.SpaceId);

        foreach (var space in spaces.Values)
        {
            // Filter conflicts to those relevant to this space
            var spaceGroupIds = groups.Values
                .Where(g => g.SpaceId == space.Id)
                .Select(g => g.Id)
                .ToHashSet();

            // Only include conflicts where at least one assignment is in this space
            var spaceConflicts = result.Conflicts
                .Where(c => spaceGroupIds.Contains(c.A.GroupId) || spaceGroupIds.Contains(c.B.GroupId))
                .ToList();

            if (spaceConflicts.Count == 0) continue;

            // Check for existing unread notification with same fingerprint (Req 8.2)
            var existingUnread = await _db.Notifications
                .AnyAsync(n =>
                    n.UserId == userId &&
                    n.SpaceId == space.Id &&
                    n.EventType == EventType &&
                    n.DeduplicationHash == dedupHash &&
                    !n.IsRead, ct);

            if (existingUnread) continue; // Skip — duplicate suppression (Req 8.2)

            // Build metadata JSON with cross-space privacy (Req 6.5)
            var metadataJson = BuildMetadataJson(spaceConflicts, spaceGroupIds);

            // Get localized text
            var (title, body) = ConflictNotificationText.Get(space.Locale);

            // Create notification
            var notification = Notification.CreateWithDedup(
                spaceId: space.Id,
                userId: userId,
                eventType: EventType,
                title: title,
                body: body,
                metadataJson: metadataJson,
                deduplicationHash: dedupHash);

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(ct);

            // Send push notification (best-effort, Req 5.7, 5.8)
            try
            {
                await _pushSender.SendPushToUserAsync(
                    userId, space.Id,
                    new PushPayload(title, body, Tag: "conflict"),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Push notification delivery failed for user {UserId} in space {SpaceId}. In-app notification retained.",
                    userId, space.Id);
            }
        }
    }

    /// <summary>
    /// Computes a SHA-256 deduplication fingerprint from sorted assignment pair IDs.
    /// Format: "min(idA,idB):max(idA,idB)|..." sorted lexicographically, then hashed.
    /// </summary>
    internal static string ComputeDeduplicationHash(IReadOnlyList<ConflictPair> conflicts)
    {
        // Step 1: For each pair, order IDs as min:max
        var pairs = conflicts
            .Select(c =>
            {
                var idA = c.A.AssignmentId.ToString();
                var idB = c.B.AssignmentId.ToString();
                return string.CompareOrdinal(idA, idB) <= 0
                    ? $"{idA}:{idB}"
                    : $"{idB}:{idA}";
            })
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        // Step 2: Concatenate with pipe separator
        var input = string.Join("|", pairs);

        // Step 3: SHA-256 hash → hex string (64 chars)
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Builds the notification metadata JSON respecting cross-space privacy (Req 6.5).
    /// Each space's notification only includes group names from that space.
    /// </summary>
    private static string BuildMetadataJson(
        IReadOnlyList<ConflictPair> conflicts,
        HashSet<Guid> spaceGroupIds)
    {
        var conflictEntries = conflicts.Select(c => new
        {
            type = c.Type == ConflictType.Overlap ? "overlap" : "rest_violation",
            slotA = new
            {
                id = c.A.TaskSlotId.ToString(),
                groupName = spaceGroupIds.Contains(c.A.GroupId) ? c.A.GroupName : null,
                startsAt = c.A.StartsAt.ToString("O"),
                endsAt = c.A.EndsAt.ToString("O")
            },
            slotB = new
            {
                id = c.B.TaskSlotId.ToString(),
                groupName = spaceGroupIds.Contains(c.B.GroupId) ? c.B.GroupName : null,
                startsAt = c.B.StartsAt.ToString("O"),
                endsAt = c.B.EndsAt.ToString("O")
            }
        }).ToList();

        return JsonSerializer.Serialize(new { conflicts = conflictEntries },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
