using Jobuler.Application.Common;
using Jobuler.Application.Conflicts;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jobuler.Application.Scheduling.Commands;

public record PublishVersionCommand(
    Guid SpaceId,
    Guid VersionId,
    Guid RequestingUserId) : IRequest;

public class PublishVersionCommandHandler : IRequestHandler<PublishVersionCommand>
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;
    private readonly IScheduleNotificationSender? _scheduleNotifications;
    private readonly IConfiguration _config;
    private readonly ILogger<PublishVersionCommandHandler> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAssignmentSnapshotService _snapshotService;
    private readonly ICumulativeTracker _cumulativeTracker;
    private readonly ICacheService _cache;

    public PublishVersionCommandHandler(
        AppDbContext db,
        IAuditLogger audit,
        IConfiguration config,
        ILogger<PublishVersionCommandHandler> logger,
        IServiceScopeFactory scopeFactory,
        IAssignmentSnapshotService snapshotService,
        ICumulativeTracker cumulativeTracker,
        ICacheService cache,
        IScheduleNotificationSender? scheduleNotifications = null)
    {
        _db = db;
        _audit = audit;
        _config = config;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _snapshotService = snapshotService;
        _cumulativeTracker = cumulativeTracker;
        _cache = cache;
        _scheduleNotifications = scheduleNotifications;
    }

    public async Task Handle(PublishVersionCommand req, CancellationToken ct)
    {
        // Set RLS session variables so queries on tenant-scoped tables work correctly.
        // Skipped when using an in-memory provider (e.g. unit tests).
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                req.SpaceId.ToString(), req.RequestingUserId.ToString());
        }

        var version = await _db.ScheduleVersions
            .FirstOrDefaultAsync(v => v.Id == req.VersionId && v.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Schedule version not found.");

        // Guard: already published — idempotent, just return
        if (version.Status == ScheduleVersionStatus.Published)
            return;

        // Guard: not a draft — cannot publish
        if (version.Status != ScheduleVersionStatus.Draft)
            throw new InvalidOperationException($"Cannot publish a version with status '{version.Status}'.");

        // Archive the current published version before publishing the new one
        var currentPublished = await _db.ScheduleVersions
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .ToListAsync(ct);

        foreach (var old in currentPublished)
            old.Archive();

        // Publish enforces draft-only rule inside the domain entity
        version.Publish(req.RequestingUserId);
        await _db.SaveChangesAsync(ct);

        // ── Invalidate cached schedule and status for all groups in this space ──
        await _cache.RemoveByPatternAsync($"schedule:{req.SpaceId}:*", ct);
        await _cache.RemoveByPatternAsync($"status:{req.SpaceId}:*", ct);

        // ── Home-leave presence windows ───────────────────────────────────────
        // If the version's SummaryJson contains home_leave_assignments, create
        // derived AtHome presence windows for each valid entry.
        await CreateHomeLeavePresenceWindowsAsync(req.SpaceId, version, ct);

        // ── Cumulative tracking: create snapshots and update counters ─────────
        await _snapshotService.CreateSnapshotsAsync(req.SpaceId, req.VersionId, ct);
        await _cumulativeTracker.UpdateOnPublishAsync(req.SpaceId, req.VersionId, ct);

        // Send in-app notifications to all space members
        var memberUserIds = await _db.SpaceMemberships.AsNoTracking()
            .Where(m => m.SpaceId == req.SpaceId)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        // Locale-aware notification text
        var space = await _db.Spaces.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == req.SpaceId, ct);
        var locale = space?.Locale ?? "he";

        var (notifTitle, notifBody) = locale switch
        {
            "he" => ($"סידור חדש פורסם", $"גרסה {version.VersionNumber} פורסמה ומוכנה לצפייה."),
            "ru" => ($"Новое расписание опубликовано", $"Версия {version.VersionNumber} опубликована и доступна для просмотра."),
            _ => ($"New schedule published", $"Schedule version {version.VersionNumber} has been published and is ready to view.")
        };

        foreach (var userId in memberUserIds)
        {
            _db.Notifications.Add(Notification.Create(
                req.SpaceId, userId,
                "schedule.published",
                notifTitle,
                notifBody,
                System.Text.Json.JsonSerializer.Serialize(new { versionId = req.VersionId })));
        }
        await _db.SaveChangesAsync(ct);

        // Audit log — do this BEFORE the fire-and-forget so it uses the same DbContext
        // sequentially (no concurrency risk).
        var afterJson = version.SourceType == "regeneration"
            ? JsonSerializer.Serialize(new
            {
                version_number = version.VersionNumber,
                supersedes_version_id = version.SupersedesVersionId,
                regeneration_run_id = version.SourceRunId,
                published_by_user_id = req.RequestingUserId
            })
            : $"{{\"version_number\":{version.VersionNumber}}}";

        await _audit.LogAsync(
            req.SpaceId, req.RequestingUserId,
            "publish_schedule",
            "schedule_version", req.VersionId,
            afterJson: afterJson,
            ct: ct);

        // Send WhatsApp/email notifications to group members (fire-and-forget, non-blocking).
        // IMPORTANT: uses a NEW scope + DbContext so it doesn't race with the current request's
        // DbContext instance (EF Core DbContext is not thread-safe).
        if (_scheduleNotifications is not null)
        {
            var versionNumber = version.VersionNumber;
            _ = Task.Run(async () =>
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var notificationSender = scope.ServiceProvider.GetRequiredService<IScheduleNotificationSender>();
                await SendExternalNotificationsAsync(req, versionNumber, db, notificationSender, CancellationToken.None);
            });
        }

        // Fire-and-forget: cross-group conflict detection
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var conflictService = scope.ServiceProvider.GetRequiredService<IConflictDetectionService>();
                await conflictService.DetectOnPublishAsync(req.SpaceId, req.VersionId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                using var scope = _scopeFactory.CreateScope();
                scope.ServiceProvider.GetRequiredService<ILogger<PublishVersionCommandHandler>>()
                    .LogError(ex, "Conflict detection failed for version {VersionId}", req.VersionId);
            }
        });
    }

    /// <summary>
    /// Sends WhatsApp/email notifications to all group members who have a phone or email.
    /// Runs fire-and-forget so it doesn't block the publish response.
    /// Uses its own DbContext scope — never shares the request's DbContext.
    /// </summary>
    private async Task SendExternalNotificationsAsync(
        PublishVersionCommand req, int versionNumber,
        AppDbContext db, IScheduleNotificationSender notificationSender,
        CancellationToken ct)
    {
        try
        {
            // Set RLS for the new scope's DbContext
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT set_config('app.current_space_id', {0}, TRUE)",
                    req.SpaceId.ToString());
            }

            var frontendUrl = _config["App:FrontendBaseUrl"] ?? "https://shifter.app";

            // Load all people in this space who have a linked user (registered members)
            var members = await db.People.AsNoTracking()
                .Where(p => p.SpaceId == req.SpaceId && p.IsActive && p.LinkedUserId != null)
                .ToListAsync(ct);

            var userIds = members.Select(p => p.LinkedUserId!.Value).ToList();
            var users = await db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, ct);

            // Get the group name for the notification message (use first active group in space)
            var group = await db.Groups.AsNoTracking()
                .Where(g => g.SpaceId == req.SpaceId && g.IsActive && g.DeletedAt == null)
                .OrderBy(g => g.CreatedAt)
                .FirstOrDefaultAsync(ct);
            var groupName = group?.Name ?? "Shifter";

            var scheduleUrl = $"{frontendUrl}/groups";

            foreach (var person in members)
            {
                if (person.LinkedUserId is null) continue;
                if (!users.TryGetValue(person.LinkedUserId.Value, out var user)) continue;

                // Prefer phone (WhatsApp), fall back to email
                var contact = !string.IsNullOrWhiteSpace(person.PhoneNumber)
                    ? person.PhoneNumber
                    : user.Email;

                if (string.IsNullOrWhiteSpace(contact)) continue;

                try
                {
                    await notificationSender.SendSchedulePublishedAsync(
                        contact,
                        person.DisplayName ?? person.FullName,
                        groupName,
                        scheduleUrl,
                        user.PreferredLocale ?? "he",
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to send schedule publish notification to {Contact}", contact);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending external notifications for published schedule version {VersionId}",
                req.VersionId);
        }
    }

    /// <summary>
    /// Reads home-leave assignments from the version's SummaryJson and creates
    /// derived AtHome presence windows. Validates no overlap with existing OnMission
    /// windows. Discards entries with unknown person_id or invalid time ranges.
    /// </summary>
    private async Task CreateHomeLeavePresenceWindowsAsync(
        Guid spaceId, ScheduleVersion version, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(version.SummaryJson))
            return;

        // Parse home_leave_assignments from SummaryJson
        List<HomeLeaveEntry>? homeLeaveEntries;
        try
        {
            using var doc = JsonDocument.Parse(version.SummaryJson);
            if (!doc.RootElement.TryGetProperty("home_leave_assignments", out var hlArray)
                || hlArray.ValueKind != JsonValueKind.Array
                || hlArray.GetArrayLength() == 0)
                return;

            homeLeaveEntries = hlArray.Deserialize<List<HomeLeaveEntry>>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse home_leave_assignments from SummaryJson for version {VersionId}",
                version.Id);
            return;
        }

        if (homeLeaveEntries is null || homeLeaveEntries.Count == 0)
            return;

        // Load all person IDs in this space for validation
        var spacePersonIdsList = await _db.People.AsNoTracking()
            .Where(p => p.SpaceId == spaceId)
            .Select(p => p.Id)
            .ToListAsync(ct);
        var spacePersonIds = spacePersonIdsList.ToHashSet();

        // Collect valid entries
        var validEntries = new List<(Guid PersonId, DateTime StartsAt, DateTime EndsAt)>();
        foreach (var entry in homeLeaveEntries)
        {
            // Validate person_id
            if (!Guid.TryParse(entry.PersonId, out var personId) || !spacePersonIds.Contains(personId))
            {
                _logger.LogWarning(
                    "Home-leave assignment discarded: unknown person_id '{PersonId}' in version {VersionId}",
                    entry.PersonId, version.Id);
                continue;
            }

            // Parse and validate time range
            if (!DateTime.TryParse(entry.StartsAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startsAt)
                || !DateTime.TryParse(entry.EndsAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var endsAt))
            {
                _logger.LogWarning(
                    "Home-leave assignment discarded: invalid date format for person {PersonId} in version {VersionId}",
                    entry.PersonId, version.Id);
                continue;
            }

            if (startsAt >= endsAt)
            {
                _logger.LogWarning(
                    "Home-leave assignment discarded: starts_at >= ends_at for person {PersonId} ({StartsAt} >= {EndsAt}) in version {VersionId}",
                    entry.PersonId, startsAt, endsAt, version.Id);
                continue;
            }

            validEntries.Add((personId, startsAt, endsAt));
        }

        if (validEntries.Count == 0)
            return;

        var affectedPersonIds = validEntries.Select(e => e.PersonId).Distinct().ToList();

        // ── Protection: only remove stale DERIVED AtHome windows for people who
        // appear in this version's home_leave_assignments. Manual windows (IsDerived = false)
        // are NEVER touched. Windows for people NOT in affectedPersonIds are NEVER touched.
        // Scoping per-person ensures we only remove derived windows that overlap with
        // that specific person's new entries — not a global time range across all people.
        // We never modify StartsAt or EndsAt of any existing window.
        var staleAtHomeWindows = new List<PresenceWindow>();
        foreach (var personId in affectedPersonIds)
        {
            var personEntries = validEntries.Where(e => e.PersonId == personId).ToList();
            var personMinStart = personEntries.Min(e => e.StartsAt);
            var personMaxEnd = personEntries.Max(e => e.EndsAt);

            var personStaleWindows = await _db.PresenceWindows
                .Where(pw => pw.SpaceId == spaceId
                    && pw.State == PresenceState.AtHome
                    && pw.IsDerived == true
                    && pw.PersonId == personId
                    && pw.StartsAt < personMaxEnd
                    && pw.EndsAt > personMinStart)
                .ToListAsync(ct);

            staleAtHomeWindows.AddRange(personStaleWindows);
        }

        if (staleAtHomeWindows.Count > 0)
        {
            _db.PresenceWindows.RemoveRange(staleAtHomeWindows);
            _logger.LogInformation(
                "Removed {Count} stale derived AtHome windows for version {VersionId}",
                staleAtHomeWindows.Count, version.Id);
        }

        var minStart = validEntries.Min(e => e.StartsAt);
        var maxEnd = validEntries.Max(e => e.EndsAt);

        // Check for overlaps with existing on_mission presence windows.
        // Only check MANUAL (non-derived) on_mission windows — derived ones from
        // previous schedule versions should not block new home-leave assignments,
        // since the new schedule supersedes the old one.

        var existingOnMissionWindows = await _db.PresenceWindows.AsNoTracking()
            .Where(pw => pw.SpaceId == spaceId
                && pw.State == PresenceState.OnMission
                && !pw.IsDerived
                && affectedPersonIds.Contains(pw.PersonId)
                && pw.StartsAt < maxEnd
                && pw.EndsAt > minStart)
            .ToListAsync(ct);

        // Also check for existing MANUAL AtHome windows to prevent duplicates
        // (derived ones were already cleaned up above)
        var existingAtHomeWindows = await _db.PresenceWindows.AsNoTracking()
            .Where(pw => pw.SpaceId == spaceId
                && pw.State == PresenceState.AtHome
                && !pw.IsDerived
                && affectedPersonIds.Contains(pw.PersonId)
                && pw.StartsAt < maxEnd
                && pw.EndsAt > minStart)
            .ToListAsync(ct);

        // Check each valid entry for overlap with on_mission OR existing at_home windows
        var nonConflictingEntries = new List<(Guid PersonId, DateTime StartsAt, DateTime EndsAt)>();
        foreach (var entry in validEntries)
        {
            var conflictingMission = existingOnMissionWindows.FirstOrDefault(pw =>
                pw.PersonId == entry.PersonId
                && pw.StartsAt < entry.EndsAt
                && pw.EndsAt > entry.StartsAt);

            if (conflictingMission is not null)
            {
                _logger.LogWarning(
                    "Home-leave window skipped (conflicts with on_mission): person {PersonId} at {StartsAt:O} – {EndsAt:O}",
                    entry.PersonId, entry.StartsAt, entry.EndsAt);
                continue;
            }

            // Skip if an AtHome window already exists for this person overlapping this time
            var duplicateAtHome = existingAtHomeWindows.FirstOrDefault(pw =>
                pw.PersonId == entry.PersonId
                && pw.StartsAt < entry.EndsAt
                && pw.EndsAt > entry.StartsAt);

            if (duplicateAtHome is not null)
            {
                _logger.LogInformation(
                    "Home-leave window skipped (duplicate): person {PersonId} already has AtHome at {StartsAt:O} – {EndsAt:O}",
                    entry.PersonId, entry.StartsAt, entry.EndsAt);
                continue;
            }

            nonConflictingEntries.Add(entry);
        }

        // Create presence windows for non-conflicting entries
        foreach (var entry in nonConflictingEntries)
        {
            var presenceWindow = PresenceWindow.CreateDerivedAtHome(
                spaceId, entry.PersonId, entry.StartsAt, entry.EndsAt);
            _db.PresenceWindows.Add(presenceWindow);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created {Count} AtHome presence windows for published version {VersionId}",
            validEntries.Count, version.Id);
    }

    /// <summary>DTO for deserializing home-leave entries from SummaryJson.</summary>
    private sealed class HomeLeaveEntry
    {
        [JsonPropertyName("person_id")] public string PersonId { get; init; } = "";
        [JsonPropertyName("starts_at")] public string StartsAt { get; init; } = "";
        [JsonPropertyName("ends_at")] public string EndsAt { get; init; } = "";
    }
}
