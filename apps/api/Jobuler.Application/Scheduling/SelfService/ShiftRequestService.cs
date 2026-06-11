using Jobuler.Application.Common;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService.Models;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Processes shift request submissions and cancellations for self-service scheduling.
/// Acquires PostgreSQL advisory locks before reading slot capacity to ensure concurrency safety.
/// Validates request window, slot status, capacity, Max_Shifts, and duplicate constraints.
/// </summary>
public class ShiftRequestService : IShiftRequestService
{
    private readonly AppDbContext _db;
    private readonly ISlotLockService _slotLockService;
    private readonly IWaitlistService? _waitlistService;
    private readonly INotificationService _notificationService;
    private readonly IPushNotificationSender _pushSender;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ShiftRequestService> _logger;

    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    public ShiftRequestService(
        AppDbContext db,
        ISlotLockService slotLockService,
        TimeProvider timeProvider,
        ILogger<ShiftRequestService> logger,
        INotificationService notificationService,
        IPushNotificationSender pushSender,
        IAuditLogger audit,
        IWaitlistService? waitlistService = null)
    {
        _db = db;
        _slotLockService = slotLockService;
        _timeProvider = timeProvider;
        _logger = logger;
        _notificationService = notificationService;
        _pushSender = pushSender;
        _audit = audit;
        _waitlistService = waitlistService;
    }

    /// <inheritdoc />
    public async Task<ShiftRequestResult> ProcessRequestAsync(Guid personId, Guid shiftSlotId, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Acquire advisory lock on the slot before reading capacity (Req 4.4, 11.1)
                var lockAcquired = await _slotLockService.TryAcquireSlotLockAsync(shiftSlotId, LockTimeout, ct);
                if (!lockAcquired)
                {
                    await transaction.RollbackAsync(ct);
                    return new ShiftRequestResult(
                        Success: false,
                        ShiftRequestId: null,
                        RejectionReason: "The slot is temporarily unavailable. Please try again shortly.",
                        AlternativeSlots: null);
                }

                // Load the slot
                var slot = await _db.ShiftSlots
                    .FirstOrDefaultAsync(s => s.Id == shiftSlotId, ct);

                // Req 4.7: Slot must exist and be Open
                if (slot is null || slot.Status != ShiftSlotStatus.Open)
                {
                    await transaction.RollbackAsync(ct);
                    return new ShiftRequestResult(
                        Success: false,
                        ShiftRequestId: null,
                        RejectionReason: "The requested slot is unavailable.",
                        AlternativeSlots: null);
                }

                // Load the scheduling cycle to validate request window (Req 6.3, 6.4)
                var cycle = await _db.SchedulingCycles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == slot.SchedulingCycleId && c.GroupId == slot.GroupId, ct);

                if (cycle is null)
                {
                    await transaction.RollbackAsync(ct);
                    return new ShiftRequestResult(
                        Success: false,
                        ShiftRequestId: null,
                        RejectionReason: "The scheduling cycle for this slot could not be found.",
                        AlternativeSlots: null);
                }

                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

                // Req 6.3: Reject if before request window opens
                if (utcNow < cycle.RequestWindowOpensAt)
                {
                    await transaction.RollbackAsync(ct);
                    return new ShiftRequestResult(
                        Success: false,
                        ShiftRequestId: null,
                        RejectionReason: $"The request window is not yet open. Requests will be accepted starting {cycle.RequestWindowOpensAt:u}.",
                        AlternativeSlots: null);
                }

                // Req 6.4: Reject if after request window closes
                if (utcNow > cycle.RequestWindowClosesAt)
                {
                    await transaction.RollbackAsync(ct);
                    return new ShiftRequestResult(
                        Success: false,
                        ShiftRequestId: null,
                        RejectionReason: "The request window has closed.",
                        AlternativeSlots: null);
                }

                // Req 4.6: Check for duplicate request (Pending or Approved on same slot by same person)
                var hasDuplicate = await _db.ShiftRequests
                    .AnyAsync(r => r.ShiftSlotId == shiftSlotId
                                   && r.PersonId == personId
                                   && (r.Status == ShiftRequestStatus.Pending || r.Status == ShiftRequestStatus.Approved), ct);

                if (hasDuplicate)
                {
                    await transaction.RollbackAsync(ct);
                    return new ShiftRequestResult(
                        Success: false,
                        ShiftRequestId: null,
                        RejectionReason: "You already have an active request for this slot.",
                        AlternativeSlots: null);
                }

                var assignmentConflict = await ShiftAssignmentSafety.FindApprovedAssignmentConflictAsync(_db, personId, slot, ct);
                if (assignmentConflict != ShiftAssignmentConflictKind.None)
                {
                    await transaction.RollbackAsync(ct);
                    return new ShiftRequestResult(
                        Success: false,
                        ShiftRequestId: null,
                        RejectionReason: assignmentConflict == ShiftAssignmentConflictKind.Overlap
                            ? "This shift overlaps with an existing approved shift."
                            : "This shift does not leave enough rest time after an existing approved shift.",
                        AlternativeSlots: null);
                }

                // Req 4.5: Check Max_Shifts constraint
                var config = await _db.SelfServiceConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.GroupId == slot.GroupId, ct);

                var maxShifts = config?.MaxShiftsPerCycle ?? 7;

                var currentShiftCount = await _db.ShiftRequests
                    .CountAsync(r => r.PersonId == personId
                                     && r.SchedulingCycleId == slot.SchedulingCycleId
                                     && (r.Status == ShiftRequestStatus.Pending || r.Status == ShiftRequestStatus.Approved), ct);

                if (currentShiftCount >= maxShifts)
                {
                    await transaction.RollbackAsync(ct);
                    return new ShiftRequestResult(
                        Success: false,
                        ShiftRequestId: null,
                        RejectionReason: $"You have reached the maximum number of shifts ({maxShifts}) for this scheduling cycle.",
                        AlternativeSlots: null);
                }

                // Req 4.1, 4.2: Check capacity
                if (!slot.HasAvailableCapacity())
                {
                    // Req 4.3: Reject with up to 5 alternative slots for the same day
                    var alternatives = await GetAlternativeSlotsAsync(personId, slot, ct);

                    await transaction.RollbackAsync(ct);

                    // Req 13.2: Send notification for rejected request (full capacity)
                    await SendRequestRejectedNotificationAsync(personId, slot, ct);

                    return new ShiftRequestResult(
                        Success: false,
                        ShiftRequestId: null,
                        RejectionReason: "The slot is at full capacity.",
                        AlternativeSlots: alternatives);
                }

                // All validations passed — approve the request
                var shiftRequest = ShiftRequest.Create(
                    spaceId: slot.SpaceId,
                    shiftSlotId: slot.Id,
                    personId: personId,
                    groupId: slot.GroupId,
                    schedulingCycleId: slot.SchedulingCycleId);

                shiftRequest.Approve();
                slot.IncrementFillCount();

                _db.ShiftRequests.Add(shiftRequest);
                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Shift request {RequestId} approved for person {PersonId} on slot {SlotId}",
                    shiftRequest.Id, personId, shiftSlotId);

                // Req 13.1: Send notification for approved request
                await SendRequestApprovedNotificationAsync(personId, slot, shiftRequest.Id, ct);

                return new ShiftRequestResult(
                    Success: true,
                    ShiftRequestId: shiftRequest.Id,
                    RejectionReason: null,
                    AlternativeSlots: null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Error processing shift request for person {PersonId} on slot {SlotId}", personId, shiftSlotId);
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<CancellationResult> CancelRequestAsync(Guid personId, Guid shiftRequestId, string reason, CancellationToken ct = default)
    {
        // Req 8.5: Validate cancellation reason length (1-500 characters)
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 1 || reason.Length > 500)
        {
            return new CancellationResult(
                Success: false,
                ErrorMessage: "Cancellation reason must be between 1 and 500 characters.");
        }

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                // Load the shift request
                var shiftRequest = await _db.ShiftRequests
                    .FirstOrDefaultAsync(r => r.Id == shiftRequestId && r.PersonId == personId, ct);

                if (shiftRequest is null)
                {
                    await transaction.RollbackAsync(ct);
                    return new CancellationResult(
                        Success: false,
                        ErrorMessage: "Shift request not found.");
                }

                // Req 8.6: Only approved requests can be cancelled
                if (shiftRequest.Status != ShiftRequestStatus.Approved)
                {
                    await transaction.RollbackAsync(ct);
                    return new CancellationResult(
                        Success: false,
                        ErrorMessage: "Only approved requests may be cancelled.");
                }

                var actorUserId = await _db.People
                    .AsNoTracking()
                    .Where(p => p.Id == personId && p.SpaceId == shiftRequest.SpaceId)
                    .Select(p => p.LinkedUserId)
                    .FirstOrDefaultAsync(ct);

                // Load the associated slot to check cancellation window
                var slot = await _db.ShiftSlots
                    .FirstOrDefaultAsync(s => s.Id == shiftRequest.ShiftSlotId, ct);

                if (slot is null)
                {
                    await transaction.RollbackAsync(ct);
                    return new CancellationResult(
                        Success: false,
                        ErrorMessage: "The associated shift slot could not be found.");
                }

                if (!SlotMatchesRequest(shiftRequest, slot))
                {
                    await transaction.RollbackAsync(ct);
                    return new CancellationResult(
                        Success: false,
                        ErrorMessage: "Shift request metadata no longer matches its assigned slot.");
                }

                // Load the scheduling cycle to check if request window is closed
                var cycle = await _db.SchedulingCycles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == slot.SchedulingCycleId && c.GroupId == slot.GroupId, ct);

                // Load config for cancellation cutoff hours
                var config = await _db.SelfServiceConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.GroupId == slot.GroupId, ct);

                var cancellationCutoffHours = config?.CancellationCutoffHours ?? 24;

                // Req 8.2: Reject if request window is closed AND current time is past cutoff
                // Cutoff = shift_start - CancellationCutoffHours
                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

                if (cycle is not null && utcNow > cycle.RequestWindowClosesAt)
                {
                    // Request window is closed — check cancellation cutoff
                    var shiftStartUtc = slot.Date.ToDateTime(slot.StartTime, DateTimeKind.Utc);
                    var cutoffTime = shiftStartUtc.AddHours(-cancellationCutoffHours);

                    if (utcNow >= cutoffTime)
                    {
                        await transaction.RollbackAsync(ct);
                        return new CancellationResult(
                            Success: false,
                            ErrorMessage: "The cancellation window has passed. Cancellations are not allowed within " +
                                          $"{cancellationCutoffHours} hours of the shift start time.");
                    }
                }

                // All validations passed — perform the cancellation
                // Req 8.1: Set status to Cancelled, record reason and timestamp
                shiftRequest.Cancel(reason);

                // Req 8.1: Decrement slot's CurrentFillCount
                var fillCountBeforeCancel = slot.CurrentFillCount;
                slot.DecrementFillCount();
                var fillCountAfterCancel = slot.CurrentFillCount;

                await _db.SaveChangesAsync(ct);

                // Req 8.4: Check if member is now under-scheduled
                var minShifts = config?.MinShiftsPerCycle ?? 0;
                if (minShifts > 0)
                {
                    var remainingApprovedCount = await _db.ShiftRequests
                        .CountAsync(r => r.PersonId == personId
                                         && r.SchedulingCycleId == slot.SchedulingCycleId
                                         && r.Status == ShiftRequestStatus.Approved, ct);

                    if (remainingApprovedCount < minShifts)
                    {
                        _logger.LogInformation(
                            "Member {PersonId} is now under-scheduled for cycle {CycleId} " +
                            "(approved: {ApprovedCount}, min required: {MinShifts}).",
                            personId, slot.SchedulingCycleId, remainingApprovedCount, minShifts);

                        // The scheduled under-scheduled-members job sends admin/member notifications
                        // after request windows close, avoiding noisy alerts during active selection.
                    }
                }

                await _audit.LogAsync(
                    shiftRequest.SpaceId,
                    actorUserId,
                    "self_service.cancel_shift",
                    "shift_request",
                    shiftRequest.Id,
                    beforeJson: JsonSerializer.Serialize(new
                    {
                        shift_request_id = shiftRequest.Id,
                        shift_request_status = "approved",
                        shift_slot_id = slot.Id,
                        person_id = personId,
                        group_id = shiftRequest.GroupId,
                        scheduling_cycle_id = shiftRequest.SchedulingCycleId,
                        fill_count = fillCountBeforeCancel
                    }),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        shift_request_id = shiftRequest.Id,
                        shift_request_status = "cancelled",
                        shift_slot_id = slot.Id,
                        person_id = personId,
                        group_id = shiftRequest.GroupId,
                        scheduling_cycle_id = shiftRequest.SchedulingCycleId,
                        cancellation_reason = shiftRequest.CancellationReason,
                        fill_count = fillCountAfterCancel,
                        waitlist_processing_requested = _waitlistService is not null
                    }),
                    ct: ct);

                // Req 8.3: Trigger waitlist processing if slot now has capacity.
                // Keep this after audit so an audit failure cannot cascade a rolled-back release.
                if (_waitlistService is not null)
                {
                    await _waitlistService.ProcessSlotReleasedAsync(slot.Id, ct);
                }
                else
                {
                    _logger.LogDebug(
                        "Waitlist service not available. Skipping waitlist processing for slot {SlotId} after cancellation.",
                        slot.Id);
                }

                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Shift request {RequestId} cancelled by person {PersonId} for slot {SlotId}. Reason: {Reason}",
                    shiftRequestId, personId, slot.Id, reason);

                await SendShiftCancelledNotificationAsync(personId, slot, shiftRequest.Id, reason, ct);

                return new CancellationResult(
                    Success: true,
                    ErrorMessage: null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex,
                    "Error cancelling shift request {RequestId} for person {PersonId}",
                    shiftRequestId, personId);
                throw;
            }
        });
    }

    /// <inheritdoc />
    public async Task<AbsenceReportResult> ReportCannotAttendAsync(
        Guid personId,
        Guid shiftRequestId,
        string reason,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            return new AbsenceReportResult(
                Success: false,
                AbsenceReportId: null,
                WasLate: false,
                LateReportsUsed: 0,
                MaxLateReports: 0,
                ErrorMessage: "Reason must be between 1 and 500 characters.");
        }

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var shiftRequest = await _db.ShiftRequests
                    .FirstOrDefaultAsync(r => r.Id == shiftRequestId && r.PersonId == personId, ct);

                if (shiftRequest is null)
                {
                    await transaction.RollbackAsync(ct);
                    return new AbsenceReportResult(false, null, false, 0, 0, "Shift request not found.");
                }

                if (shiftRequest.Status != ShiftRequestStatus.Approved)
                {
                    await transaction.RollbackAsync(ct);
                    return new AbsenceReportResult(false, null, false, 0, 0, "Only approved shifts can be reported as cannot attend.");
                }

                var existingReport = await _db.ShiftAbsenceReports
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ShiftRequestId == shiftRequestId, ct);

                if (existingReport is not null)
                {
                    await transaction.RollbackAsync(ct);
                    return new AbsenceReportResult(false, null, existingReport.IsLate, 0, 0, "This shift already has an absence report.");
                }

                var slot = await _db.ShiftSlots
                    .FirstOrDefaultAsync(s => s.Id == shiftRequest.ShiftSlotId, ct);

                if (slot is null)
                {
                    await transaction.RollbackAsync(ct);
                    return new AbsenceReportResult(false, null, false, 0, 0, "The associated shift slot could not be found.");
                }

                if (!SlotMatchesRequest(shiftRequest, slot))
                {
                    await transaction.RollbackAsync(ct);
                    return new AbsenceReportResult(false, null, false, 0, 0, "Shift request metadata no longer matches its assigned slot.");
                }

                var actorUserId = await _db.People
                    .AsNoTracking()
                    .Where(p => p.Id == personId && p.SpaceId == shiftRequest.SpaceId)
                    .Select(p => p.LinkedUserId)
                    .FirstOrDefaultAsync(ct);

                var config = await _db.SelfServiceConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.GroupId == slot.GroupId, ct);

                var maxLateReports = config?.MaxLateCancellationsPerCycle ?? 2;
                var lateWindowHours = config?.LateCancellationWindowHours ?? config?.CancellationCutoffHours ?? 24;
                var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
                var shiftStartUtc = slot.Date.ToDateTime(slot.StartTime, DateTimeKind.Utc);

                if (shiftStartUtc <= utcNow)
                {
                    await transaction.RollbackAsync(ct);
                    return new AbsenceReportResult(false, null, true, 0, maxLateReports, "This shift has already started.");
                }

                var wasLate = utcNow >= shiftStartUtc.AddHours(-lateWindowHours);
                var lateReportsUsed = 0;

                if (wasLate)
                {
                    lateReportsUsed = await _db.ShiftAbsenceReports
                        .CountAsync(r => r.PersonId == personId
                                         && r.SchedulingCycleId == shiftRequest.SchedulingCycleId
                                         && r.IsLate
                                         && r.Status != ShiftAbsenceReportStatus.Rejected, ct);

                    if (lateReportsUsed >= maxLateReports)
                    {
                        await transaction.RollbackAsync(ct);
                        return new AbsenceReportResult(
                            false,
                            null,
                            true,
                            lateReportsUsed,
                            maxLateReports,
                            $"You have reached the late absence limit ({maxLateReports}) for this scheduling cycle.");
                    }
                }

                var absenceReport = ShiftAbsenceReport.Create(
                    shiftRequest.SpaceId,
                    shiftRequest.GroupId,
                    shiftRequest.SchedulingCycleId,
                    shiftRequest.Id,
                    slot.Id,
                    personId,
                    reason,
                    wasLate,
                    utcNow);

                _db.ShiftAbsenceReports.Add(absenceReport);

                var fillCountBeforeAbsence = slot.CurrentFillCount;
                shiftRequest.Cancel(wasLate
                    ? $"Late absence report: {reason.Trim()}"
                    : $"Cannot attend: {reason.Trim()}");
                slot.DecrementFillCount();
                var fillCountAfterAbsence = slot.CurrentFillCount;

                await _db.SaveChangesAsync(ct);

                await _audit.LogAsync(
                    shiftRequest.SpaceId,
                    actorUserId,
                    "self_service.report_absence",
                    "shift_absence_report",
                    absenceReport.Id,
                    beforeJson: JsonSerializer.Serialize(new
                    {
                        shift_request_id = shiftRequest.Id,
                        shift_request_status = "approved",
                        shift_slot_id = slot.Id,
                        person_id = personId,
                        group_id = shiftRequest.GroupId,
                        scheduling_cycle_id = shiftRequest.SchedulingCycleId,
                        fill_count = fillCountBeforeAbsence
                    }),
                    afterJson: JsonSerializer.Serialize(new
                    {
                        absence_report_id = absenceReport.Id,
                        shift_request_id = shiftRequest.Id,
                        shift_request_status = "cancelled",
                        shift_slot_id = slot.Id,
                        person_id = personId,
                        group_id = shiftRequest.GroupId,
                        scheduling_cycle_id = shiftRequest.SchedulingCycleId,
                        was_late = wasLate,
                        late_reports_used = wasLate ? lateReportsUsed + 1 : lateReportsUsed,
                        max_late_reports = maxLateReports,
                        late_window_hours = lateWindowHours,
                        fill_count = fillCountAfterAbsence,
                        waitlist_processing_requested = _waitlistService is not null
                    }),
                    ct: ct);

                // Keep waitlist cascade after audit so audit failure cannot offer a rolled-back seat.
                if (_waitlistService is not null)
                {
                    await _waitlistService.ProcessSlotReleasedAsync(slot.Id, ct);
                }

                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Shift absence report {ReportId} created for person {PersonId}, request {RequestId}. Late={WasLate}",
                    absenceReport.Id, personId, shiftRequestId, wasLate);

                await SendAbsenceReportedNotificationAsync(personId, slot, absenceReport.Id, reason, wasLate, ct);

                return new AbsenceReportResult(
                    true,
                    absenceReport.Id,
                    wasLate,
                    wasLate ? lateReportsUsed + 1 : lateReportsUsed,
                    maxLateReports,
                    null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex,
                    "Error reporting cannot attend for request {RequestId}, person {PersonId}",
                    shiftRequestId, personId);
                throw;
            }
        });
    }

    private static bool SlotMatchesRequest(ShiftRequest request, ShiftSlot slot) =>
        request.SpaceId == slot.SpaceId
        && request.GroupId == slot.GroupId
        && request.SchedulingCycleId == slot.SchedulingCycleId;

    /// <summary>
    /// Returns up to 5 alternative available slots for the same day as the target slot.
    /// Excludes slots the member already has a request for and slots that overlap
    /// or violate rest windows against their approved shifts.
    /// </summary>
    private async Task<IReadOnlyList<AvailableSlotDto>> GetAlternativeSlotsAsync(
        Guid personId, ShiftSlot targetSlot, CancellationToken ct)
    {
        // Load open slots on the same day with remaining capacity (excluding the target slot)
        var sameDaySlots = await _db.ShiftSlots
            .AsNoTracking()
            .Where(s => s.GroupId == targetSlot.GroupId
                        && s.SchedulingCycleId == targetSlot.SchedulingCycleId
                        && s.Date == targetSlot.Date
                        && s.Id != targetSlot.Id
                        && s.Status == ShiftSlotStatus.Open
                        && s.CurrentFillCount < s.Capacity)
            .Join(
                _db.GroupTasks.AsNoTracking(),
                slot => slot.GroupTaskId,
                task => task.Id,
                (slot, task) => new { Slot = slot, TaskName = task.Name })
            .ToListAsync(ct);

        if (sameDaySlots.Count == 0)
            return [];

        // Load the member's existing pending/approved request slot IDs for this cycle
        var memberRequestedSlotIds = await _db.ShiftRequests
            .AsNoTracking()
            .Where(r => r.PersonId == personId
                        && r.SchedulingCycleId == targetSlot.SchedulingCycleId
                        && (r.Status == ShiftRequestStatus.Pending || r.Status == ShiftRequestStatus.Approved))
            .Select(r => r.ShiftSlotId)
            .ToListAsync(ct);

        var memberRequestedSlotIdSet = memberRequestedSlotIds.ToHashSet();

        var alternatives = new List<AvailableSlotDto>();

        foreach (var item in sameDaySlots)
        {
            var slot = item.Slot;

            // Exclude slots the member already has a request for
            if (memberRequestedSlotIdSet.Contains(slot.Id))
                continue;

            var assignmentConflict = await ShiftAssignmentSafety.FindApprovedAssignmentConflictAsync(
                _db,
                personId,
                slot,
                ct);

            if (assignmentConflict != ShiftAssignmentConflictKind.None)
                continue;

            alternatives.Add(new AvailableSlotDto(
                ShiftSlotId: slot.Id,
                Date: slot.Date,
                StartTime: slot.StartTime,
                EndTime: slot.EndTime,
                TaskName: item.TaskName,
                CurrentFillCount: slot.CurrentFillCount,
                Capacity: slot.Capacity));

            if (alternatives.Count >= 5)
                break;
        }

        // Sort by start time ascending
        return alternatives.OrderBy(a => a.StartTime).ToList();
    }

    /// <summary>
    /// Sends an in-app and push notification when a shift request is approved (Req 13.1).
    /// Push failures are logged but do not affect in-app notification persistence (Req 13.7).
    /// </summary>
    private async Task SendRequestApprovedNotificationAsync(
        Guid personId, ShiftSlot slot, Guid shiftRequestId, CancellationToken ct)
    {
        try
        {
            var person = await _db.People
                .AsNoTracking()
                .Where(p => p.Id == personId && p.SpaceId == slot.SpaceId)
                .Select(p => new { p.LinkedUserId })
                .FirstOrDefaultAsync(ct);

            if (person?.LinkedUserId is null)
                return;

            var taskName = await _db.GroupTasks
                .AsNoTracking()
                .Where(t => t.Id == slot.GroupTaskId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct) ?? "Shift";

            var title = "Shift Request Approved";
            var body = $"Your request for {taskName} on {slot.Date:MMM dd} ({slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}) has been approved.";

            var notification = Notification.Create(
                spaceId: slot.SpaceId,
                userId: person.LinkedUserId.Value,
                eventType: "self_service.request_approved",
                title: title,
                body: body,
                metadataJson: JsonSerializer.Serialize(new
                {
                    shiftRequestId,
                    shiftSlotId = slot.Id,
                    groupId = slot.GroupId,
                    date = slot.Date,
                    startTime = slot.StartTime.ToString("HH:mm"),
                    endTime = slot.EndTime.ToString("HH:mm"),
                    taskName
                }));

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(ct);

            // Attempt push notification — failure does not affect in-app (Req 13.7)
            try
            {
                var payload = new PushPayload(
                    Title: title,
                    Body: body,
                    Icon: "/favicon.jpeg",
                    Url: "/shifts");

                await _pushSender.SendPushToUserAsync(person.LinkedUserId.Value, slot.SpaceId, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Push notification delivery failed for request approved (person {PersonId}, slot {SlotId}). " +
                    "In-app notification was persisted successfully.",
                    personId, slot.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send request approved notification for person {PersonId}, slot {SlotId}",
                personId, slot.Id);
        }
    }

    /// <summary>
    /// Sends an in-app and push notification when a shift request is rejected due to full capacity (Req 13.2).
    /// Includes a link to the available shifts list for the same date range.
    /// Push failures are logged but do not affect in-app notification persistence (Req 13.7).
    /// </summary>
    private async Task SendRequestRejectedNotificationAsync(
        Guid personId, ShiftSlot slot, CancellationToken ct)
    {
        try
        {
            var person = await _db.People
                .AsNoTracking()
                .Where(p => p.Id == personId && p.SpaceId == slot.SpaceId)
                .Select(p => new { p.LinkedUserId })
                .FirstOrDefaultAsync(ct);

            if (person?.LinkedUserId is null)
                return;

            var taskName = await _db.GroupTasks
                .AsNoTracking()
                .Where(t => t.Id == slot.GroupTaskId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct) ?? "Shift";

            var title = "Shift Request Rejected";
            var body = $"Your request for {taskName} on {slot.Date:MMM dd} ({slot.StartTime:HH:mm}–{slot.EndTime:HH:mm}) " +
                       "was rejected because the slot is at full capacity. View available shifts for alternative options.";

            var notification = Notification.Create(
                spaceId: slot.SpaceId,
                userId: person.LinkedUserId.Value,
                eventType: "self_service.request_rejected",
                title: title,
                body: body,
                metadataJson: JsonSerializer.Serialize(new
                {
                    shiftSlotId = slot.Id,
                    groupId = slot.GroupId,
                    schedulingCycleId = slot.SchedulingCycleId,
                    date = slot.Date,
                    startTime = slot.StartTime.ToString("HH:mm"),
                    endTime = slot.EndTime.ToString("HH:mm"),
                    taskName,
                    reason = "full_capacity",
                    availableSlotsUrl = $"/shifts?groupId={slot.GroupId}&cycleId={slot.SchedulingCycleId}"
                }));

            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(ct);

            // Attempt push notification — failure does not affect in-app (Req 13.7)
            try
            {
                var payload = new PushPayload(
                    Title: title,
                    Body: body,
                    Icon: "/favicon.jpeg",
                    Url: $"/shifts?groupId={slot.GroupId}&cycleId={slot.SchedulingCycleId}");

                await _pushSender.SendPushToUserAsync(person.LinkedUserId.Value, slot.SpaceId, payload, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Push notification delivery failed for request rejected (person {PersonId}, slot {SlotId}). " +
                    "In-app notification was persisted successfully.",
                    personId, slot.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send request rejected notification for person {PersonId}, slot {SlotId}",
                personId, slot.Id);
        }
    }

    private async Task SendAbsenceReportedNotificationAsync(
        Guid personId,
        ShiftSlot slot,
        Guid absenceReportId,
        string reason,
        bool wasLate,
        CancellationToken ct)
    {
        try
        {
            var personName = await _db.People
                .AsNoTracking()
                .Where(p => p.Id == personId && p.SpaceId == slot.SpaceId)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync(ct) ?? "A member";

            var taskName = await _db.GroupTasks
                .AsNoTracking()
                .Where(t => t.Id == slot.GroupTaskId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct) ?? "Shift";

            var title = wasLate ? "Late Absence Reported" : "Absence Reported";
            var body = $"{personName} reported they cannot attend {taskName} on {slot.Date:MMM dd} ({slot.StartTime:HH:mm}-{slot.EndTime:HH:mm}).";

            await _notificationService.NotifySpaceAdminsAsync(
                slot.SpaceId,
                eventType: "self_service.absence_reported",
                title,
                body,
                metadataJson: JsonSerializer.Serialize(new
                {
                    absenceReportId,
                    personId,
                    personName,
                    groupId = slot.GroupId,
                    shiftSlotId = slot.Id,
                    schedulingCycleId = slot.SchedulingCycleId,
                    date = slot.Date,
                    startTime = slot.StartTime.ToString("HH:mm"),
                    endTime = slot.EndTime.ToString("HH:mm"),
                    taskName,
                    reason = reason.Trim(),
                    wasLate
                }),
                groupId: slot.GroupId,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send absence reported notification for report {ReportId}, person {PersonId}",
                absenceReportId, personId);
        }
    }

    private async Task SendShiftCancelledNotificationAsync(
        Guid personId,
        ShiftSlot slot,
        Guid shiftRequestId,
        string reason,
        CancellationToken ct)
    {
        try
        {
            var personName = await _db.People
                .AsNoTracking()
                .Where(p => p.Id == personId && p.SpaceId == slot.SpaceId)
                .Select(p => p.FullName)
                .FirstOrDefaultAsync(ct) ?? "A member";

            var taskName = await _db.GroupTasks
                .AsNoTracking()
                .Where(t => t.Id == slot.GroupTaskId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct) ?? "Shift";

            var title = "Shift Cancelled";
            var body = $"{personName} cancelled {taskName} on {slot.Date:MMM dd} ({slot.StartTime:HH:mm}-{slot.EndTime:HH:mm}).";

            await _notificationService.NotifySpaceAdminsAsync(
                slot.SpaceId,
                "self_service.shift_cancelled",
                title,
                body,
                JsonSerializer.Serialize(new
                {
                    shiftRequestId,
                    shiftSlotId = slot.Id,
                    groupId = slot.GroupId,
                    schedulingCycleId = slot.SchedulingCycleId,
                    personId,
                    personName,
                    date = slot.Date,
                    startTime = slot.StartTime.ToString("HH:mm"),
                    endTime = slot.EndTime.ToString("HH:mm"),
                    taskName,
                    reason
                }),
                slot.GroupId,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send shift cancelled notification for person {PersonId}, slot {SlotId}",
                personId,
                slot.Id);
        }
    }
}
