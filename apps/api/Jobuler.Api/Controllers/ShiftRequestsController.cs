using Jobuler.Api.Middleware;
using Jobuler.Application.Common;
using Jobuler.Application.Notifications;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Scheduling.SelfService.Queries;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/shift-requests")]
[Authorize]
public class ShiftRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;
    private readonly IShiftRequestService _shiftRequestService;
    private readonly IPushNotificationSender _pushSender;
    private readonly IAuditLogger _audit;
    private readonly AppDbContext _db;

    public ShiftRequestsController(
        IMediator mediator,
        IPermissionService permissions,
        IShiftRequestService shiftRequestService,
        IPushNotificationSender pushSender,
        IAuditLogger audit,
        AppDbContext db)
    {
        _mediator = mediator;
        _permissions = permissions;
        _shiftRequestService = shiftRequestService;
        _pushSender = pushSender;
        _audit = audit;
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Submit a shift request for the current member on a specific slot.
    /// The member is resolved from the authenticated user's linked person in the space.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Submit(
        Guid spaceId, Guid groupId,
        [FromBody] SubmitShiftRequestRequest req,
        CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, groupId, ct);
        if (personId is null)
            return Forbid();

        if (!await ShiftSlotBelongsToGroupAsync(spaceId, groupId, req.ShiftSlotId, ct))
            return NotFound();

        var result = await _shiftRequestService.ProcessRequestAsync(personId.Value, req.ShiftSlotId, ct);

        if (!result.Success)
        {
            var extensions = result.AlternativeSlots is not null
                ? new Dictionary<string, object?> { ["alternativeSlots"] = result.AlternativeSlots }
                : null;

            return ProblemDetailsResults.Problem(
                HttpContext,
                statusCode: 422,
                title: "Unprocessable Entity",
                detail: result.RejectionReason!,
                typeSlug: "shift-request-rejected",
                extensions: extensions);
        }

        return Created("", new ShiftRequestSuccessResponse(
            ShiftRequestId: result.ShiftRequestId!.Value));
    }

    /// <summary>
    /// Cancel a previously approved shift request for the current member.
    /// Requires a cancellation reason between 1 and 500 characters.
    /// </summary>
    [HttpPost("{shiftRequestId:guid}/cancel")]
    public async Task<IActionResult> Cancel(
        Guid spaceId, Guid groupId, Guid shiftRequestId,
        [FromBody] CancelShiftRequestRequest req,
        CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, groupId, ct);
        if (personId is null)
            return Forbid();

        if (!await ShiftRequestBelongsToGroupAsync(spaceId, groupId, shiftRequestId, personId.Value, ct))
            return NotFound();

        var result = await _shiftRequestService.CancelRequestAsync(personId.Value, shiftRequestId, req.Reason, ct);

        if (!result.Success)
        {
            return ProblemDetailsResults.Problem(
                HttpContext,
                statusCode: 422,
                title: "Unprocessable Entity",
                detail: result.ErrorMessage!,
                typeSlug: "shift-request-rejected");
        }

        return NoContent();
    }

    /// <summary>
    /// Report that the current member cannot attend an approved shift.
    /// Late reports are counted against the group's configured per-cycle limit.
    /// </summary>
    [HttpPost("{shiftRequestId:guid}/cannot-attend")]
    public async Task<IActionResult> CannotAttend(
        Guid spaceId, Guid groupId, Guid shiftRequestId,
        [FromBody] CannotAttendShiftRequest req,
        CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, groupId, ct);
        if (personId is null)
            return Forbid();

        if (!await ShiftRequestBelongsToGroupAsync(spaceId, groupId, shiftRequestId, personId.Value, ct))
            return NotFound();

        var result = await _shiftRequestService.ReportCannotAttendAsync(personId.Value, shiftRequestId, req.Reason, ct);

        if (!result.Success)
        {
            return ProblemDetailsResults.Problem(
                HttpContext,
                statusCode: 422,
                title: "Unprocessable Entity",
                detail: result.ErrorMessage!,
                typeSlug: "shift-absence-rejected",
                extensions: new Dictionary<string, object?>
                {
                    ["wasLate"] = result.WasLate,
                    ["absenceReportsUsed"] = result.AbsenceReportsUsed,
                    ["maxAbsenceReports"] = result.MaxAbsenceReports,
                    ["lateReportsUsed"] = result.LateReportsUsed,
                    ["maxLateReports"] = result.MaxLateReports
                });
        }

        return Created("", new CannotAttendShiftResponse(
            result.AbsenceReportId!.Value,
            result.WasLate,
            result.AbsenceReportsUsed,
            result.MaxAbsenceReports,
            result.LateReportsUsed,
            result.MaxLateReports));
    }

    /// <summary>
    /// List the current member's absence reports for this group.
    /// Defaults to the current/upcoming cycle so late-report usage matches the per-cycle limit.
    /// </summary>
    [HttpGet("absence-reports/mine")]
    public async Task<IActionResult> ListMyAbsenceReports(
        Guid spaceId,
        Guid groupId,
        [FromQuery] string? cycleId,
        CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, groupId, ct);
        if (personId is null)
            return Forbid();

        var resolvedCycleId = await ResolveCycleIdAsync(spaceId, groupId, cycleId ?? "current", ct);
        if (resolvedCycleId == Guid.Empty && string.Equals(cycleId ?? "current", "current", StringComparison.OrdinalIgnoreCase))
        {
            var configOnly = await _db.SelfServiceConfigs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.GroupId == groupId, ct);

            return Ok(new MyAbsenceReportsResponse(
                Array.Empty<AbsenceReportResponse>(),
                AbsenceReportsUsed: 0,
                MaxAbsenceReports: configOnly?.MaxAbsencesPerCycle ?? 3,
                LateReportsUsed: 0,
                MaxLateReports: configOnly?.MaxLateCancellationsPerCycle ?? 2,
                SchedulingCycleId: null));
        }

        if (resolvedCycleId == Guid.Empty)
            return BadRequest(new { error = "Invalid cycleId. Use a scheduling cycle id or 'current'." });

        var reports = await _db.ShiftAbsenceReports
            .AsNoTracking()
            .Where(r => r.SpaceId == spaceId
                        && r.GroupId == groupId
                        && r.PersonId == personId.Value
                        && r.SchedulingCycleId == resolvedCycleId)
            .OrderByDescending(r => r.ReportedAt)
            .Join(_db.People.AsNoTracking(), r => r.PersonId, p => p.Id, (r, p) => new { Report = r, PersonName = p.DisplayName ?? p.FullName })
            .Join(_db.ShiftSlots.AsNoTracking(), rp => rp.Report.ShiftSlotId, s => s.Id, (rp, s) => new { rp.Report, rp.PersonName, Slot = s })
            .Join(_db.GroupTasks.AsNoTracking(), rps => rps.Slot.GroupTaskId, t => t.Id, (rps, t) => new { rps.Report, rps.PersonName, rps.Slot, TaskName = t.Name })
            .Select(r => new AbsenceReportResponse(
                r.Report.Id,
                r.Report.ShiftRequestId,
                r.Report.PersonId,
                r.PersonName,
                r.Report.ShiftSlotId,
                r.Slot.Date,
                r.Slot.StartTime,
                r.Slot.EndTime,
                r.TaskName,
                r.Report.Reason,
                r.Report.IsLate,
                r.Report.Status.ToString(),
                r.Report.ReportedAt,
                r.Report.AdminNote,
                r.Report.ReviewedAt))
            .ToListAsync(ct);

        var lateReportsUsed = await _db.ShiftAbsenceReports
            .AsNoTracking()
            .CountAsync(r => r.SpaceId == spaceId
                             && r.GroupId == groupId
                             && r.PersonId == personId.Value
                             && r.SchedulingCycleId == resolvedCycleId
                             && r.IsLate
                             && r.Status != ShiftAbsenceReportStatus.Rejected,
                ct);

        var absenceReportsUsed = await _db.ShiftAbsenceReports
            .AsNoTracking()
            .CountAsync(r => r.SpaceId == spaceId
                             && r.GroupId == groupId
                             && r.PersonId == personId.Value
                             && r.SchedulingCycleId == resolvedCycleId
                             && r.Status != ShiftAbsenceReportStatus.Rejected,
                ct);

        var config = await _db.SelfServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.GroupId == groupId, ct);

        return Ok(new MyAbsenceReportsResponse(
            reports,
            absenceReportsUsed,
            config?.MaxAbsencesPerCycle ?? 3,
            lateReportsUsed,
            config?.MaxLateCancellationsPerCycle ?? 2,
            resolvedCycleId));
    }

    /// <summary>List absence reports for this group for admin review.</summary>
    [HttpGet("absence-reports")]
    public async Task<IActionResult> ListAbsenceReports(
        Guid spaceId,
        Guid groupId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var query = _db.ShiftAbsenceReports
            .AsNoTracking()
            .Where(r => r.SpaceId == spaceId && r.GroupId == groupId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ShiftAbsenceReportStatus>(status, true, out var parsedStatus))
                return BadRequest(new { error = "Invalid absence report status." });

            query = query.Where(r => r.Status == parsedStatus);
        }

        var reports = await query
            .OrderByDescending(r => r.ReportedAt)
            .Join(_db.People.AsNoTracking(), r => r.PersonId, p => p.Id, (r, p) => new { Report = r, PersonName = p.DisplayName ?? p.FullName })
            .Join(_db.ShiftSlots.AsNoTracking(), rp => rp.Report.ShiftSlotId, s => s.Id, (rp, s) => new { rp.Report, rp.PersonName, Slot = s })
            .Join(_db.GroupTasks.AsNoTracking(), rps => rps.Slot.GroupTaskId, t => t.Id, (rps, t) => new AbsenceReportResponse(
                rps.Report.Id,
                rps.Report.ShiftRequestId,
                rps.Report.PersonId,
                rps.PersonName,
                rps.Report.ShiftSlotId,
                rps.Slot.Date,
                rps.Slot.StartTime,
                rps.Slot.EndTime,
                t.Name,
                rps.Report.Reason,
                rps.Report.IsLate,
                rps.Report.Status.ToString(),
                rps.Report.ReportedAt,
                rps.Report.AdminNote,
                rps.Report.ReviewedAt))
            .ToListAsync(ct);

        return Ok(reports);
    }

    [HttpPost("absence-reports/{absenceReportId:guid}/approve")]
    public async Task<IActionResult> ApproveAbsenceReport(
        Guid spaceId,
        Guid groupId,
        Guid absenceReportId,
        [FromBody] ReviewAbsenceReportRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var report = await _db.ShiftAbsenceReports
            .FirstOrDefaultAsync(r => r.Id == absenceReportId && r.SpaceId == spaceId && r.GroupId == groupId, ct);

        if (report is null)
            return NotFound();

        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            var push = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(ct);

                try
                {
                    report.Approve(CurrentUserId, req.AdminNote);
                    await _db.SaveChangesAsync(ct);
                    await _audit.LogAsync(
                        spaceId,
                        CurrentUserId,
                        "self_service.approve_absence_report",
                        "shift_absence_report",
                        report.Id,
                        beforeJson: JsonSerializer.Serialize(new
                        {
                            absence_report_id = report.Id,
                            shift_request_id = report.ShiftRequestId,
                            person_id = report.PersonId,
                            group_id = report.GroupId,
                            scheduling_cycle_id = report.SchedulingCycleId,
                            shift_slot_id = report.ShiftSlotId,
                            is_late = report.IsLate,
                            status = "pending"
                        }),
                        afterJson: JsonSerializer.Serialize(new
                        {
                            absence_report_id = report.Id,
                            shift_request_id = report.ShiftRequestId,
                            person_id = report.PersonId,
                            group_id = report.GroupId,
                            scheduling_cycle_id = report.SchedulingCycleId,
                            shift_slot_id = report.ShiftSlotId,
                            is_late = report.IsLate,
                            status = report.Status.ToString().ToLowerInvariant(),
                            admin_note = report.AdminNote
                        }),
                        ct: ct);
                    var push = await AddAbsenceReviewNotificationAsync(report, approved: true, ct);
                    await _db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return push;
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            });
            await SendAbsenceReviewPushAsync(push, ct);
        }
        catch (InvalidOperationException ex)
        {
            return AbsenceRejected(ex.Message);
        }

        return NoContent();
    }

    [HttpPost("absence-reports/{absenceReportId:guid}/reject")]
    public async Task<IActionResult> RejectAbsenceReport(
        Guid spaceId,
        Guid groupId,
        Guid absenceReportId,
        [FromBody] ReviewAbsenceReportRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var report = await _db.ShiftAbsenceReports
            .FirstOrDefaultAsync(r => r.Id == absenceReportId && r.SpaceId == spaceId && r.GroupId == groupId, ct);

        if (report is null)
            return NotFound();

        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            var push = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(ct);

                try
                {
                    var shiftRequest = await _db.ShiftRequests
                        .FirstOrDefaultAsync(r => r.Id == report.ShiftRequestId
                                                  && r.SpaceId == spaceId
                                                  && r.GroupId == groupId,
                            ct);
                    var slot = await _db.ShiftSlots
                        .FirstOrDefaultAsync(s => s.Id == report.ShiftSlotId
                                                  && s.SpaceId == spaceId
                                                  && s.GroupId == groupId,
                            ct);

                    var reinstatedShift = false;
                    int? fillCountBeforeReinstatement = null;
                    int? fillCountAfterReinstatement = null;

                    if (shiftRequest is not null && slot is not null && shiftRequest.Status == ShiftRequestStatus.Cancelled)
                    {
                        if (!slot.HasAvailableCapacity())
                        {
                            throw new InvalidOperationException("Cannot reject this absence report because the released shift slot is already full.");
                        }

                        fillCountBeforeReinstatement = slot.CurrentFillCount;
                        shiftRequest.ReinstateRejectedAbsenceCancellation();
                        slot.IncrementFillCount();
                        fillCountAfterReinstatement = slot.CurrentFillCount;
                        reinstatedShift = true;
                    }

                    report.Reject(CurrentUserId, req.AdminNote);
                    await _db.SaveChangesAsync(ct);
                    await _audit.LogAsync(
                        spaceId,
                        CurrentUserId,
                        "self_service.reject_absence_report",
                        "shift_absence_report",
                        report.Id,
                        beforeJson: JsonSerializer.Serialize(new
                        {
                            absence_report_id = report.Id,
                            shift_request_id = report.ShiftRequestId,
                            person_id = report.PersonId,
                            group_id = report.GroupId,
                            scheduling_cycle_id = report.SchedulingCycleId,
                            shift_slot_id = report.ShiftSlotId,
                            is_late = report.IsLate,
                            status = "pending"
                        }),
                        afterJson: JsonSerializer.Serialize(new
                        {
                            absence_report_id = report.Id,
                            shift_request_id = report.ShiftRequestId,
                            person_id = report.PersonId,
                            group_id = report.GroupId,
                            scheduling_cycle_id = report.SchedulingCycleId,
                            shift_slot_id = report.ShiftSlotId,
                            is_late = report.IsLate,
                            status = report.Status.ToString().ToLowerInvariant(),
                            admin_note = report.AdminNote,
                            reinstated_shift = reinstatedShift,
                            shift_request_status = shiftRequest?.Status.ToString().ToLowerInvariant(),
                            fill_count_before_reinstatement = fillCountBeforeReinstatement,
                            fill_count_after_reinstatement = fillCountAfterReinstatement
                        }),
                        ct: ct);
                    var push = await AddAbsenceReviewNotificationAsync(report, approved: false, ct);
                    await _db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return push;
                }
                catch
                {
                    await transaction.RollbackAsync(ct);
                    throw;
                }
            });
            await SendAbsenceReviewPushAsync(push, ct);
        }
        catch (InvalidOperationException ex)
        {
            return AbsenceRejected(ex.Message);
        }

        return NoContent();
    }

    /// <summary>
    /// List shift requests in this group for admin review/activity surfaces.
    /// </summary>
    [HttpGet("admin")]
    public async Task<IActionResult> ListAdmin(
        Guid spaceId,
        Guid groupId,
        [FromQuery] string? status,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        ShiftRequestStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ShiftRequestStatus>(status, true, out var nextStatus))
                return BadRequest(new { error = "Invalid shift request status." });

            parsedStatus = nextStatus;
        }

        limit = Math.Clamp(limit, 1, 100);

        var query = _db.ShiftRequests
            .AsNoTracking()
            .Where(r => r.SpaceId == spaceId && r.GroupId == groupId);

        if (parsedStatus.HasValue)
            query = query.Where(r => r.Status == parsedStatus.Value);

        var requests = await query
            .Join(
                _db.People.AsNoTracking(),
                r => r.PersonId,
                p => p.Id,
                (r, p) => new { Request = r, PersonName = p.DisplayName ?? p.FullName })
            .Join(
                _db.ShiftSlots.AsNoTracking(),
                rp => rp.Request.ShiftSlotId,
                s => s.Id,
                (rp, s) => new { rp.Request, rp.PersonName, Slot = s })
            .Join(
                _db.GroupTasks.AsNoTracking(),
                rps => rps.Slot.GroupTaskId,
                t => t.Id,
                (rps, t) => new { rps.Request, rps.PersonName, rps.Slot, TaskName = t.Name })
            .OrderByDescending(x => x.Request.CancelledAt ?? x.Request.CreatedAt)
            .Take(limit)
            .Select(x => new AdminShiftRequestResponse(
                x.Request.Id,
                x.Request.ShiftSlotId,
                x.Request.PersonId,
                x.PersonName,
                x.Request.GroupId,
                x.Request.SchedulingCycleId,
                x.Slot.Date,
                x.Slot.StartTime,
                x.Slot.EndTime,
                x.TaskName,
                x.Request.Status.ToString(),
                x.Request.IsAdminOverride,
                x.Request.RejectionReason,
                x.Request.CancellationReason,
                x.Request.CancelledAt,
                x.Request.CreatedAt,
                false))
            .ToListAsync(ct);

        return Ok(requests);
    }

    [HttpPost("admin/{shiftRequestId:guid}/attendance")]
    public async Task<IActionResult> RecordAttendance(
        Guid spaceId,
        Guid groupId,
        Guid shiftRequestId,
        [FromBody] RecordShiftAttendanceRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        if (!Enum.TryParse<ShiftAttendanceStatus>(req.Status, true, out var attendanceStatus))
            return BadRequest(new { error = "Invalid attendance status." });

        var shift = await _db.ShiftRequests
            .FirstOrDefaultAsync(r => r.Id == shiftRequestId
                                      && r.SpaceId == spaceId
                                      && r.GroupId == groupId, ct);

        if (shift is null)
            return NotFound();

        if (shift.Status != ShiftRequestStatus.Approved)
            return BadRequest(new { error = "Attendance can only be recorded for approved shift assignments." });

        var slot = await _db.ShiftSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shift.ShiftSlotId
                                      && s.SpaceId == spaceId
                                      && s.GroupId == groupId, ct);

        if (slot is null)
            return NotFound();

        if (slot.StartsAt > DateTime.UtcNow)
            return BadRequest(new { error = "Attendance cannot be recorded before the shift starts." });

        ShiftAttendanceStatus? previousStatus = null;
        var record = await _db.ShiftAttendanceRecords
            .FirstOrDefaultAsync(r => r.ShiftRequestId == shiftRequestId
                                      && r.SpaceId == spaceId
                                      && r.GroupId == groupId, ct);

        if (record is null)
        {
            record = ShiftAttendanceRecord.Create(
                spaceId,
                groupId,
                shift.SchedulingCycleId,
                shift.Id,
                shift.ShiftSlotId,
                shift.PersonId,
                attendanceStatus,
                CurrentUserId,
                req.Note);
            _db.ShiftAttendanceRecords.Add(record);
        }
        else
        {
            previousStatus = record.Status;
            record.Update(attendanceStatus, CurrentUserId, req.Note);
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(
            spaceId,
            CurrentUserId,
            "self_service.record_shift_attendance",
            "shift_attendance_record",
            record.Id,
            beforeJson: previousStatus.HasValue
                ? JsonSerializer.Serialize(new { status = previousStatus.Value.ToString().ToLowerInvariant() })
                : null,
            afterJson: JsonSerializer.Serialize(new
            {
                attendance_record_id = record.Id,
                shift_request_id = shift.Id,
                person_id = shift.PersonId,
                group_id = shift.GroupId,
                scheduling_cycle_id = shift.SchedulingCycleId,
                shift_slot_id = shift.ShiftSlotId,
                status = record.Status.ToString().ToLowerInvariant(),
                note = record.Note
            }),
            ct: ct);

        return Ok(new ShiftAttendanceResponse(
            record.Id,
            record.ShiftRequestId,
            record.PersonId,
            record.ShiftSlotId,
            record.SchedulingCycleId,
            record.Status.ToString(),
            record.Note,
            record.RecordedByUserId,
            record.RecordedAt));
    }

    /// <summary>
    /// List the current member's shift requests for a group, optionally filtered by scheduling cycle.
    /// </summary>
    [HttpGet("mine")]
    public async Task<IActionResult> ListMine(
        Guid spaceId, Guid groupId,
        [FromQuery] Guid? schedulingCycleId,
        CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, groupId, ct);
        if (personId is null)
            return Forbid();

        var result = await _mediator.Send(
            new GetMyShiftRequestsQuery(spaceId, groupId, personId.Value, schedulingCycleId), ct);

        var currentShiftCount = result.Count(r => r.Status == "Approved");
        var config = await _db.SelfServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.GroupId == groupId, ct);
        var cycleIds = result
            .Select(r => r.SchedulingCycleId)
            .Distinct()
            .ToList();
        var now = DateTime.UtcNow;
        var requestWindowByCycle = await _db.SchedulingCycles
            .AsNoTracking()
            .Where(c => cycleIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.RequestWindowOpensAt,
                c.RequestWindowClosesAt
            })
            .ToDictionaryAsync(
                c => c.Id,
                c => now >= c.RequestWindowOpensAt && now <= c.RequestWindowClosesAt,
                ct);

        return Ok(new MyShiftRequestsResponse(
            result.Select(r => new ShiftRequestResponse(
                r.Id,
                r.ShiftSlotId,
                r.SchedulingCycleId,
                r.Date,
                r.StartTime,
                r.EndTime,
                r.TaskName,
                r.Status,
                r.IsAdminOverride,
                r.RejectionReason,
                r.CancellationReason,
                r.CancelledAt,
                r.CreatedAt,
                requestWindowByCycle.GetValueOrDefault(r.SchedulingCycleId, false))).ToList(),
            currentShiftCount,
            config?.MinShiftsPerCycle ?? 0,
            config?.MaxShiftsPerCycle ?? 7,
            config?.CancellationCutoffHours ?? 24,
            config?.MaxLateCancellationsPerCycle ?? 2,
            config?.LateCancellationWindowHours ?? config?.CancellationCutoffHours ?? 24));
    }

    /// <summary>
    /// Resolves the current authenticated user's person ID within the given space.
    /// Returns null if the user has no linked person in this space.
    /// </summary>
    private async Task<Guid?> ResolvePersonIdAsync(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var personId = await _db.People
            .AsNoTracking()
            .Where(p => p.SpaceId == spaceId && p.LinkedUserId == CurrentUserId)
            .Join(
                _db.GroupMemberships.AsNoTracking()
                    .Where(gm => gm.SpaceId == spaceId && gm.GroupId == groupId),
                p => p.Id,
                gm => gm.PersonId,
                (p, _) => p.Id)
            .FirstOrDefaultAsync(ct);

        return personId == Guid.Empty ? null : personId;
    }

    private Task<bool> ShiftSlotBelongsToGroupAsync(
        Guid spaceId,
        Guid groupId,
        Guid shiftSlotId,
        CancellationToken ct) =>
        _db.ShiftSlots
            .AsNoTracking()
            .AnyAsync(s => s.Id == shiftSlotId && s.SpaceId == spaceId && s.GroupId == groupId, ct);

    private Task<bool> ShiftRequestBelongsToGroupAsync(
        Guid spaceId,
        Guid groupId,
        Guid shiftRequestId,
        Guid personId,
        CancellationToken ct) =>
        _db.ShiftRequests
            .AsNoTracking()
            .AnyAsync(r => r.Id == shiftRequestId
                           && r.SpaceId == spaceId
                           && r.GroupId == groupId
                           && r.PersonId == personId,
                ct);

    private async Task<Guid> ResolveCycleIdAsync(Guid spaceId, Guid groupId, string cycleId, CancellationToken ct)
    {
        if (!string.Equals(cycleId, "current", StringComparison.OrdinalIgnoreCase))
            return Guid.TryParse(cycleId, out var parsedCycleId) ? parsedCycleId : Guid.Empty;

        var now = DateTime.UtcNow;
        return await _db.SchedulingCycles
            .AsNoTracking()
            .Where(c => c.SpaceId == spaceId && c.GroupId == groupId && c.EndsAt >= now)
            .OrderBy(c => c.StartsAt < now ? 0 : 1)
            .ThenBy(c => c.StartsAt)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);
    }

    private IActionResult AbsenceRejected(string detail) =>
        ProblemDetailsResults.Problem(
            HttpContext,
            statusCode: 422,
            title: "Unprocessable Entity",
            detail: detail,
            typeSlug: "shift-absence-rejected");

    private async Task<AbsenceReviewPush?> AddAbsenceReviewNotificationAsync(
        ShiftAbsenceReport report,
        bool approved,
        CancellationToken ct)
    {
        var detail = await _db.People
            .AsNoTracking()
            .Where(p => p.Id == report.PersonId && p.SpaceId == report.SpaceId && p.LinkedUserId != null)
            .Join(
                _db.ShiftSlots.AsNoTracking(),
                p => report.ShiftSlotId,
                s => s.Id,
                (p, s) => new { Person = p, Slot = s })
            .Join(
                _db.GroupTasks.AsNoTracking(),
                ps => ps.Slot.GroupTaskId,
                t => t.Id,
                (ps, t) => new { ps.Person.LinkedUserId, Slot = ps.Slot, TaskName = t.Name })
            .FirstOrDefaultAsync(ct);

        if (detail?.LinkedUserId is null)
            return null;

        var title = approved ? "Absence Report Approved" : "Absence Report Rejected";
        var body = approved
            ? $"Your absence report for {detail.TaskName} on {detail.Slot.Date:MMM dd} was approved."
            : $"Your absence report for {detail.TaskName} on {detail.Slot.Date:MMM dd} was rejected.";

        _db.Notifications.Add(Notification.Create(
            report.SpaceId,
            detail.LinkedUserId.Value,
            approved ? "self_service.absence_approved" : "self_service.absence_rejected",
            title,
            body,
            JsonSerializer.Serialize(new
            {
                absenceReportId = report.Id,
                shiftRequestId = report.ShiftRequestId,
                groupId = report.GroupId,
                shiftSlotId = report.ShiftSlotId,
                date = detail.Slot.Date,
                startTime = detail.Slot.StartTime.ToString("HH:mm"),
                endTime = detail.Slot.EndTime.ToString("HH:mm"),
                taskName = detail.TaskName,
                adminNote = report.AdminNote,
                reviewedAt = report.ReviewedAt
            })));

        return new AbsenceReviewPush(
            detail.LinkedUserId.Value,
            report.SpaceId,
            new PushPayload(title, body, "/favicon.jpeg", "/shifts"));
    }

    private async Task SendAbsenceReviewPushAsync(AbsenceReviewPush? push, CancellationToken ct)
    {
        if (push is null)
            return;

        try
        {
            await _pushSender.SendPushToUserAsync(
                push.UserId,
                push.SpaceId,
                push.Payload,
                ct);
        }
        catch
        {
            // In-app notification is the source of truth; push failures must not affect review.
        }
    }

    private sealed record AbsenceReviewPush(Guid UserId, Guid SpaceId, PushPayload Payload);
}

// --- Request DTOs ---

public record SubmitShiftRequestRequest(Guid ShiftSlotId);

public record CancelShiftRequestRequest(string Reason);

public record CannotAttendShiftRequest(string Reason);

public record RecordShiftAttendanceRequest(string Status, string? Note);

public record ReviewAbsenceReportRequest(string? AdminNote);

// --- Response DTOs ---

public record ShiftRequestSuccessResponse(Guid ShiftRequestId);

public record ShiftAttendanceResponse(
    Guid Id,
    Guid ShiftRequestId,
    Guid PersonId,
    Guid ShiftSlotId,
    Guid SchedulingCycleId,
    string Status,
    string? Note,
    Guid RecordedByUserId,
    DateTime RecordedAt);

public record MyShiftRequestsResponse(
    IReadOnlyList<ShiftRequestResponse> Requests,
    int CurrentShiftCount,
    int MinShiftsPerCycle,
    int MaxShiftsPerCycle,
    int CancellationCutoffHours,
    int MaxLateReports,
    int LateCancellationWindowHours);

public record ShiftRequestResponse(
    Guid Id,
    Guid ShiftSlotId,
    Guid SchedulingCycleId,
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    TimeOnly SlotEndTime,
    string TaskName,
    string Status,
    bool IsAdminOverride,
    string? RejectionReason,
    string? CancellationReason,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    bool RequestWindowOpen);

public record AdminShiftRequestResponse(
    Guid Id,
    Guid ShiftSlotId,
    Guid PersonId,
    string PersonName,
    Guid GroupId,
    Guid SchedulingCycleId,
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    TimeOnly SlotEndTime,
    string TaskName,
    string Status,
    bool IsAdminOverride,
    string? RejectionReason,
    string? CancellationReason,
    DateTime? CancelledAt,
    DateTime CreatedAt,
    bool RequestWindowOpen);

public record CannotAttendShiftResponse(
    Guid AbsenceReportId,
    bool WasLate,
    int AbsenceReportsUsed,
    int MaxAbsenceReports,
    int LateReportsUsed,
    int MaxLateReports);

public record AbsenceReportResponse(
    Guid Id,
    Guid ShiftRequestId,
    Guid PersonId,
    string PersonName,
    Guid ShiftSlotId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string TaskName,
    string Reason,
    bool IsLate,
    string Status,
    DateTime ReportedAt,
    string? AdminNote,
    DateTime? ReviewedAt);

public record MyAbsenceReportsResponse(
    IReadOnlyList<AbsenceReportResponse> Reports,
    int AbsenceReportsUsed,
    int MaxAbsenceReports,
    int LateReportsUsed,
    int MaxLateReports,
    Guid? SchedulingCycleId);
