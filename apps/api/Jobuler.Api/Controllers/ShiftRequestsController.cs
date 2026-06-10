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
    private readonly AppDbContext _db;

    public ShiftRequestsController(
        IMediator mediator,
        IPermissionService permissions,
        IShiftRequestService shiftRequestService,
        IPushNotificationSender pushSender,
        AppDbContext db)
    {
        _mediator = mediator;
        _permissions = permissions;
        _shiftRequestService = shiftRequestService;
        _pushSender = pushSender;
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
                    ["lateReportsUsed"] = result.LateReportsUsed,
                    ["maxLateReports"] = result.MaxLateReports
                });
        }

        return Created("", new CannotAttendShiftResponse(
            result.AbsenceReportId!.Value,
            result.WasLate,
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
            .OrderByDescending(r => r.ReportedAt)
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

        var config = await _db.SelfServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.GroupId == groupId, ct);

        return Ok(new MyAbsenceReportsResponse(
            reports,
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
            .OrderByDescending(r => r.ReportedAt)
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
            report.Approve(CurrentUserId, req.AdminNote);
            await _db.SaveChangesAsync(ct);
            await SendAbsenceReviewNotificationAsync(report, approved: true, ct);
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
            report.Reject(CurrentUserId, req.AdminNote);
            await _db.SaveChangesAsync(ct);
            await SendAbsenceReviewNotificationAsync(report, approved: false, ct);
        }
        catch (InvalidOperationException ex)
        {
            return AbsenceRejected(ex.Message);
        }

        return NoContent();
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

        var currentShiftCount = result.Count(r => r.Status is "Approved" or "Pending");
        var config = await _db.SelfServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == spaceId && c.GroupId == groupId, ct);

        return Ok(new MyShiftRequestsResponse(
            result.Select(r => new ShiftRequestResponse(
                r.Id,
                r.ShiftSlotId,
                r.Date,
                r.StartTime,
                r.EndTime,
                r.TaskName,
                r.Status,
                r.IsAdminOverride,
                r.RejectionReason,
                r.CancellationReason,
                r.CancelledAt,
                r.CreatedAt)).ToList(),
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

    private async Task SendAbsenceReviewNotificationAsync(
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
            return;

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

        await _db.SaveChangesAsync(ct);

        try
        {
            await _pushSender.SendPushToUserAsync(
                detail.LinkedUserId.Value,
                report.SpaceId,
                new PushPayload(title, body, "/favicon.jpeg", "/shifts"),
                ct);
        }
        catch
        {
            // In-app notification is the source of truth; push failures must not affect review.
        }
    }
}

// --- Request DTOs ---

public record SubmitShiftRequestRequest(Guid ShiftSlotId);

public record CancelShiftRequestRequest(string Reason);

public record CannotAttendShiftRequest(string Reason);

public record ReviewAbsenceReportRequest(string? AdminNote);

// --- Response DTOs ---

public record ShiftRequestSuccessResponse(Guid ShiftRequestId);

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
    DateOnly SlotDate,
    TimeOnly SlotStartTime,
    TimeOnly SlotEndTime,
    string TaskName,
    string Status,
    bool IsAdminOverride,
    string? RejectionReason,
    string? CancellationReason,
    DateTime? CancelledAt,
    DateTime CreatedAt);

public record CannotAttendShiftResponse(
    Guid AbsenceReportId,
    bool WasLate,
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
    int LateReportsUsed,
    int MaxLateReports,
    Guid? SchedulingCycleId);
