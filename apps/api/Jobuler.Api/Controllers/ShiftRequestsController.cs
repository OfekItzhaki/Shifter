using Jobuler.Api.Middleware;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Scheduling.SelfService.Queries;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/shift-requests")]
[Authorize]
public class ShiftRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPermissionService _permissions;
    private readonly IShiftRequestService _shiftRequestService;
    private readonly AppDbContext _db;

    public ShiftRequestsController(
        IMediator mediator,
        IPermissionService permissions,
        IShiftRequestService shiftRequestService,
        AppDbContext db)
    {
        _mediator = mediator;
        _permissions = permissions;
        _shiftRequestService = shiftRequestService;
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
        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

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
        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

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
        var personId = await ResolvePersonIdAsync(spaceId, ct);
        if (personId is null)
            return Forbid();

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

        report.Approve(CurrentUserId, req.AdminNote);
        await _db.SaveChangesAsync(ct);

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

        report.Reject(CurrentUserId, req.AdminNote);
        await _db.SaveChangesAsync(ct);

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
        var personId = await ResolvePersonIdAsync(spaceId, ct);
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
            config?.CancellationCutoffHours ?? 24));
    }

    /// <summary>
    /// Resolves the current authenticated user's person ID within the given space.
    /// Returns null if the user has no linked person in this space.
    /// </summary>
    private async Task<Guid?> ResolvePersonIdAsync(Guid spaceId, CancellationToken ct)
    {
        var personId = await _db.People
            .AsNoTracking()
            .Where(p => p.SpaceId == spaceId && p.LinkedUserId == CurrentUserId)
            .Select(p => p.Id)
            .FirstOrDefaultAsync(ct);

        return personId == Guid.Empty ? null : personId;
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
    int CancellationCutoffHours);

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
