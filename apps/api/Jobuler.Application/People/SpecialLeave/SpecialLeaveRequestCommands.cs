using System.Text.Json;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Jobuler.Domain.People;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Tasks;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.People.SpecialLeave;

public record SubmitSpecialLeaveRequestCommand(
    Guid SpaceId,
    Guid PersonId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Reason,
    Guid RequestedByUserId) : IRequest<Guid>;

public class SubmitSpecialLeaveRequestCommandHandler
    : IRequestHandler<SubmitSpecialLeaveRequestCommand, Guid>
{
    private readonly AppDbContext _db;

    public SubmitSpecialLeaveRequestCommandHandler(AppDbContext db) => _db = db;

    public async Task<Guid> Handle(SubmitSpecialLeaveRequestCommand req, CancellationToken ct)
    {
        var personExists = await _db.People.AsNoTracking()
            .AnyAsync(p => p.Id == req.PersonId
                && p.SpaceId == req.SpaceId
                && p.LinkedUserId == req.RequestedByUserId
                && p.IsActive, ct);

        if (!personExists)
            throw new UnauthorizedAccessException("Current user is not linked to this person in the space.");

        var overlapsExistingRequest = await _db.SpecialLeaveRequests.AsNoTracking()
            .AnyAsync(r => r.SpaceId == req.SpaceId
                && r.PersonId == req.PersonId
                && (r.Status == SpecialLeaveRequestStatus.Pending || r.Status == SpecialLeaveRequestStatus.Approved)
                && r.StartsAt < req.EndsAt
                && r.EndsAt > req.StartsAt, ct);

        if (overlapsExistingRequest)
            throw new InvalidOperationException("An active special leave request already overlaps this time.");

        var request = SpecialLeaveRequest.Create(
            req.SpaceId, req.PersonId, req.StartsAt, req.EndsAt, req.Reason, req.RequestedByUserId);

        _db.SpecialLeaveRequests.Add(request);
        await _db.SaveChangesAsync(ct);
        return request.Id;
    }
}

public record ApproveSpecialLeaveRequestCommand(
    Guid SpaceId,
    Guid RequestId,
    Guid ProcessedByUserId,
    string? AdminNote,
    Guid? ReasonId = null) : IRequest<ApproveSpecialLeaveRequestResult>;

public record ApproveSpecialLeaveRequestResult(
    Guid PresenceWindowId,
    IReadOnlyList<Guid> RegenerationRunIds);

public class ApproveSpecialLeaveRequestCommandHandler
    : IRequestHandler<ApproveSpecialLeaveRequestCommand, ApproveSpecialLeaveRequestResult>
{
    private readonly AppDbContext _db;
    private readonly ICumulativeTracker _cumulativeTracker;
    private readonly ICacheService _cache;
    private readonly IAuditLogger _audit;
    private readonly ISolverJobQueue _solverQueue;
    private readonly ISolverPayloadNormalizer _normalizer;
    private readonly ISolverClient _solverClient;

    public ApproveSpecialLeaveRequestCommandHandler(
        AppDbContext db,
        ICumulativeTracker cumulativeTracker,
        ICacheService cache,
        IAuditLogger audit,
        ISolverJobQueue solverQueue,
        ISolverPayloadNormalizer normalizer,
        ISolverClient solverClient)
    {
        _db = db;
        _cumulativeTracker = cumulativeTracker;
        _cache = cache;
        _audit = audit;
        _solverQueue = solverQueue;
        _normalizer = normalizer;
        _solverClient = solverClient;
    }

    public async Task<ApproveSpecialLeaveRequestResult> Handle(
        ApproveSpecialLeaveRequestCommand req,
        CancellationToken ct)
    {
        var request = await _db.SpecialLeaveRequests
            .FirstOrDefaultAsync(r => r.Id == req.RequestId && r.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Special leave request not found.");

        var publishedVersion = await GetLatestPublishedVersionAsync(req.SpaceId, ct);
        var affectedSlotsByGroup = publishedVersion is null
            ? []
            : await FindAffectedSlotsByGroupAsync(
                publishedVersion.Id,
                req.SpaceId,
                request.PersonId,
                request.StartsAt,
                request.EndsAt,
                ct);

        if (publishedVersion is not null && affectedSlotsByGroup.Count > 0)
        {
            await EnsureApprovalIsSchedulableAsync(
                request,
                publishedVersion.Id,
                affectedSlotsByGroup,
                ct);
        }

        var presence = PresenceWindow.CreateManual(
            req.SpaceId,
            request.PersonId,
            PresenceState.AtHome,
            request.StartsAt,
            request.EndsAt,
            BuildPresenceNote(request, req.AdminNote),
            req.ReasonId);

        _db.PresenceWindows.Add(presence);
        request.Approve(req.ProcessedByUserId, presence.Id, req.AdminNote);
        await _db.SaveChangesAsync(ct);

        await _cumulativeTracker.RecomputeForPersonAsync(req.SpaceId, request.PersonId, ct);
        await _cache.RemoveByPatternAsync($"status:{req.SpaceId}:*", ct);

        var regenerationRunIds = publishedVersion is null
            ? []
            : await QueueMinimalRegenerationsAsync(
                request,
                req.ProcessedByUserId,
                publishedVersion.Id,
                affectedSlotsByGroup.Keys.ToHashSet(),
                ct);

        await _audit.LogAsync(
            req.SpaceId,
            req.ProcessedByUserId,
            "approve_special_leave_request",
            "special_leave_request",
            request.Id,
            afterJson: JsonSerializer.Serialize(new
            {
                request_id = request.Id,
                person_id = request.PersonId,
                starts_at = request.StartsAt,
                ends_at = request.EndsAt,
                presence_window_id = presence.Id,
                regeneration_run_ids = regenerationRunIds
            }),
            ct: ct);

        return new ApproveSpecialLeaveRequestResult(presence.Id, regenerationRunIds);
    }

    private async Task<List<Guid>> QueueMinimalRegenerationsAsync(
        SpecialLeaveRequest request,
        Guid processedByUserId,
        Guid publishedVersionId,
        HashSet<Guid> affectedGroupIds,
        CancellationToken ct)
    {
        if (affectedGroupIds.Count == 0)
            return [];

        var regenerationRunIds = new List<Guid>();
        var regenerationStart = DateTime.SpecifyKind(request.StartsAt.Date, DateTimeKind.Utc);

        foreach (var groupId in affectedGroupIds)
        {
            var hasActiveRun = await _db.ScheduleRuns.AsNoTracking()
                .AnyAsync(r => r.SpaceId == request.SpaceId
                    && r.GroupId == groupId
                    && r.TriggerType == ScheduleRunTrigger.Regeneration
                    && (r.Status == ScheduleRunStatus.Queued || r.Status == ScheduleRunStatus.Running), ct);

            if (hasActiveRun)
                continue;

            var run = ScheduleRun.Create(
                request.SpaceId,
                ScheduleRunTrigger.Regeneration,
                publishedVersionId,
                processedByUserId,
                groupId);

            _db.ScheduleRuns.Add(run);
            await _db.SaveChangesAsync(ct);

            try
            {
                await _solverQueue.EnqueueAsync(new SolverJobMessage(
                    run.Id,
                    request.SpaceId,
                    "regeneration",
                    publishedVersionId,
                    processedByUserId,
                    groupId,
                    regenerationStart), ct);

                regenerationRunIds.Add(run.Id);
            }
            catch (Exception ex)
            {
                run.MarkFailed($"Could not queue automatic regeneration for approved special leave: {ex.Message}");
                await _db.SaveChangesAsync(ct);
            }
        }

        return regenerationRunIds;
    }

    private async Task<ScheduleVersion?> GetLatestPublishedVersionAsync(Guid spaceId, CancellationToken ct) =>
        await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

    private async Task EnsureApprovalIsSchedulableAsync(
        SpecialLeaveRequest request,
        Guid publishedVersionId,
        Dictionary<Guid, HashSet<Guid>> affectedSlotsByGroup,
        CancellationToken ct)
    {
        foreach (var (groupId, affectedSlotIds) in affectedSlotsByGroup)
        {
            var payload = await _normalizer.BuildAsync(
                request.SpaceId,
                Guid.NewGuid(),
                "preview",
                publishedVersionId,
                groupId,
                DateTime.SpecifyKind(request.StartsAt.Date, DateTimeKind.Utc),
                ct);

            var presenceWindows = payload.PresenceWindows
                .Append(new PresenceWindowDto(
                    request.PersonId.ToString(),
                    "at_home",
                    request.StartsAt.ToString("o"),
                    request.EndsAt.ToString("o")))
                .ToList();

            var previewPayload = payload with
            {
                PresenceWindows = presenceWindows,
                PreviewMode = true
            };

            SolverOutputDto result;
            try
            {
                result = await _solverClient.SolveAsync(previewPayload, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TimeoutException or OperationCanceledException)
            {
                throw new InvalidOperationException("Cannot approve this leave request because Shifter could not verify the replacement schedule.", ex);
            }

            if (!result.Feasible)
                throw new InvalidOperationException("Cannot approve this leave request because Shifter cannot preserve the schedule constraints.");

            var uncovered = result.UncoveredSlotIds
                .Select(id => Guid.TryParse(id, out var parsed) ? parsed : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToHashSet();

            if (affectedSlotIds.Any(uncovered.Contains))
                throw new InvalidOperationException("Cannot approve this leave request because Shifter cannot cover the member's affected shifts while preserving constraints.");
        }
    }

    private async Task<Dictionary<Guid, HashSet<Guid>>> FindAffectedSlotsByGroupAsync(
        Guid publishedVersionId,
        Guid spaceId,
        Guid personId,
        DateTime leaveStartsAt,
        DateTime leaveEndsAt,
        CancellationToken ct)
    {
        var assignedSlotIds = (await _db.Assignments.AsNoTracking()
            .Where(a => a.SpaceId == spaceId
                && a.ScheduleVersionId == publishedVersionId
                && a.PersonId == personId)
            .Select(a => a.TaskSlotId)
            .ToListAsync(ct)).ToHashSet();

        if (assignedSlotIds.Count == 0)
            return [];

        var affectedSlotsByGroup = new Dictionary<Guid, HashSet<Guid>>();

        var groupTasks = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.SpaceId == spaceId
                && t.IsActive
                && t.StartsAt < leaveEndsAt
                && t.EndsAt > leaveStartsAt)
            .ToListAsync(ct);

        foreach (var task in groupTasks)
        {
            if (task.ShiftDurationMinutes < 1)
                continue;

            var shiftDuration = TimeSpan.FromMinutes(task.ShiftDurationMinutes);
            var searchStart = leaveStartsAt - shiftDuration;
            if (searchStart < task.StartsAt)
                searchStart = task.StartsAt;

            var shiftIndex = (int)Math.Floor((searchStart - task.StartsAt).TotalMinutes / task.ShiftDurationMinutes);
            if (shiftIndex < 0)
                shiftIndex = 0;

            var shiftStart = task.StartsAt + TimeSpan.FromMinutes((double)shiftIndex * task.ShiftDurationMinutes);
            while (shiftStart < leaveEndsAt && shiftStart + shiftDuration <= task.EndsAt)
            {
                var shiftEnd = shiftStart + shiftDuration;
                var shiftId = ShiftGuidHelper.DeriveShiftGuid(task.Id, shiftIndex);

                if (assignedSlotIds.Contains(shiftId)
                    && shiftStart < leaveEndsAt
                    && shiftEnd > leaveStartsAt)
                {
                    if (!affectedSlotsByGroup.TryGetValue(task.GroupId, out var slotIds))
                    {
                        slotIds = [];
                        affectedSlotsByGroup[task.GroupId] = slotIds;
                    }

                    slotIds.Add(shiftId);
                }

                shiftStart = shiftEnd;
                shiftIndex++;
            }
        }

        return affectedSlotsByGroup;
    }

    private static string BuildPresenceNote(SpecialLeaveRequest request, string? adminNote)
    {
        var note = $"Approved special leave: {request.Reason}";
        if (!string.IsNullOrWhiteSpace(adminNote))
            note += $" | Admin note: {adminNote.Trim()}";
        return note.Length <= 500 ? note : note[..500];
    }

    private static class ShiftGuidHelper
    {
        internal static Guid DeriveShiftGuid(Guid taskId, int shiftIndex)
        {
            var bytes = taskId.ToByteArray();
            var indexBytes = BitConverter.GetBytes(shiftIndex);
            for (var i = 0; i < 4; i++)
                bytes[12 + i] ^= indexBytes[i];
            return new Guid(bytes);
        }
    }
}

public record RejectSpecialLeaveRequestCommand(
    Guid SpaceId,
    Guid RequestId,
    Guid ProcessedByUserId,
    string? AdminNote) : IRequest;

public class RejectSpecialLeaveRequestCommandHandler
    : IRequestHandler<RejectSpecialLeaveRequestCommand>
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;

    public RejectSpecialLeaveRequestCommandHandler(AppDbContext db, IAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task Handle(RejectSpecialLeaveRequestCommand req, CancellationToken ct)
    {
        var request = await _db.SpecialLeaveRequests
            .FirstOrDefaultAsync(r => r.Id == req.RequestId && r.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Special leave request not found.");

        request.Reject(req.ProcessedByUserId, req.AdminNote);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            req.SpaceId,
            req.ProcessedByUserId,
            "reject_special_leave_request",
            "special_leave_request",
            request.Id,
            afterJson: JsonSerializer.Serialize(new
            {
                request_id = request.Id,
                person_id = request.PersonId,
                admin_note = req.AdminNote
            }),
            ct: ct);
    }
}

public record CancelSpecialLeaveRequestCommand(
    Guid SpaceId,
    Guid RequestId,
    Guid PersonId) : IRequest;

public class CancelSpecialLeaveRequestCommandHandler
    : IRequestHandler<CancelSpecialLeaveRequestCommand>
{
    private readonly AppDbContext _db;

    public CancelSpecialLeaveRequestCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(CancelSpecialLeaveRequestCommand req, CancellationToken ct)
    {
        var request = await _db.SpecialLeaveRequests
            .FirstOrDefaultAsync(r => r.Id == req.RequestId
                && r.SpaceId == req.SpaceId
                && r.PersonId == req.PersonId, ct)
            ?? throw new KeyNotFoundException("Special leave request not found.");

        request.Cancel();
        await _db.SaveChangesAsync(ct);
    }
}
