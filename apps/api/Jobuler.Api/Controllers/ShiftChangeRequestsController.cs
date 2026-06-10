using Jobuler.Api.Middleware;
using Jobuler.Application.Common;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/shift-change-requests")]
[Authorize]
public class ShiftChangeRequestsController : ControllerBase
{
    private readonly IPermissionService _permissions;
    private readonly AppDbContext _db;

    public ShiftChangeRequestsController(IPermissionService permissions, AppDbContext db)
    {
        _permissions = permissions;
        _db = db;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, groupId, ct);
        if (personId is null)
            return Forbid();

        var rows = await BuildChangeRequestQuery(spaceId, groupId).ToListAsync(ct);

        return Ok(rows
            .Where(r => r.Change.PersonId == personId.Value)
            .OrderByDescending(r => r.Change.RequestedAt)
            .Select(r => ToDto(r))
            .ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Submit(
        Guid spaceId,
        Guid groupId,
        [FromBody] SubmitShiftChangeRequest req,
        CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, groupId, ct);
        if (personId is null)
            return Forbid();

        var shiftRequest = await _db.ShiftRequests
            .FirstOrDefaultAsync(r => r.Id == req.ShiftRequestId
                                      && r.SpaceId == spaceId
                                      && r.GroupId == groupId
                                      && r.PersonId == personId.Value
                                      && r.Status == ShiftRequestStatus.Approved,
                ct);

        if (shiftRequest is null)
            return NotFound();

        if (req.RequestedShiftSlotId.HasValue)
        {
            var requestedSlot = await _db.ShiftSlots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == req.RequestedShiftSlotId.Value
                                          && s.SpaceId == spaceId
                                          && s.GroupId == groupId,
                    ct);

            if (requestedSlot is null)
                return NotFound();

            if (requestedSlot.SchedulingCycleId != shiftRequest.SchedulingCycleId)
                return Rejected("Requested shift must be in the same scheduling cycle.");
        }

        var hasPendingChange = await _db.ShiftChangeRequests
            .AsNoTracking()
            .AnyAsync(r => r.ShiftRequestId == shiftRequest.Id
                           && r.Status == ShiftChangeRequestStatus.Pending,
                ct);

        if (hasPendingChange)
            return Rejected("A pending change request already exists for this shift.");

        try
        {
            var changeRequest = ShiftChangeRequest.Create(
                spaceId,
                groupId,
                shiftRequest.SchedulingCycleId,
                shiftRequest.Id,
                shiftRequest.ShiftSlotId,
                req.RequestedShiftSlotId,
                personId.Value,
                req.Reason,
                DateTime.UtcNow);

            _db.ShiftChangeRequests.Add(changeRequest);
            await _db.SaveChangesAsync(ct);

            return Created("", new { id = changeRequest.Id });
        }
        catch (InvalidOperationException ex)
        {
            return Rejected(ex.Message);
        }
    }

    [HttpPost("{changeRequestId:guid}/cancel")]
    public async Task<IActionResult> CancelMine(
        Guid spaceId,
        Guid groupId,
        Guid changeRequestId,
        CancellationToken ct)
    {
        var personId = await ResolvePersonIdAsync(spaceId, groupId, ct);
        if (personId is null)
            return Forbid();

        var changeRequest = await _db.ShiftChangeRequests
            .FirstOrDefaultAsync(r => r.Id == changeRequestId
                                      && r.SpaceId == spaceId
                                      && r.GroupId == groupId
                                      && r.PersonId == personId.Value,
                ct);

        if (changeRequest is null)
            return NotFound();

        try
        {
            changeRequest.Cancel();
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Rejected(ex.Message);
        }
    }

    [HttpGet("admin")]
    public async Task<IActionResult> ListForAdmin(
        Guid spaceId,
        Guid groupId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var rows = await BuildChangeRequestQuery(spaceId, groupId).ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ShiftChangeRequestStatus>(status, true, out var parsedStatus))
                return BadRequest(new { error = "Invalid shift change request status." });

            rows = rows.Where(r => r.Change.Status == parsedStatus).ToList();
        }

        return Ok(rows
            .OrderByDescending(r => r.Change.RequestedAt)
            .Select(r => ToDto(r))
            .ToList());
    }

    [HttpPost("admin/{changeRequestId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid spaceId,
        Guid groupId,
        Guid changeRequestId,
        [FromBody] ReviewShiftChangeRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var changeRequest = await _db.ShiftChangeRequests
            .FirstOrDefaultAsync(r => r.Id == changeRequestId
                                      && r.SpaceId == spaceId
                                      && r.GroupId == groupId,
                ct);

        if (changeRequest is null)
            return NotFound();

        if (changeRequest.RequestedShiftSlotId.HasValue)
        {
            var shiftRequest = await _db.ShiftRequests
                .FirstOrDefaultAsync(r => r.Id == changeRequest.ShiftRequestId
                                          && r.SpaceId == spaceId
                                          && r.GroupId == groupId
                                          && r.PersonId == changeRequest.PersonId
                                          && r.Status == ShiftRequestStatus.Approved,
                    ct);
            var originalSlot = await _db.ShiftSlots
                .FirstOrDefaultAsync(s => s.Id == changeRequest.OriginalShiftSlotId
                                          && s.SpaceId == spaceId
                                          && s.GroupId == groupId,
                    ct);
            var requestedSlot = await _db.ShiftSlots
                .FirstOrDefaultAsync(s => s.Id == changeRequest.RequestedShiftSlotId.Value
                                          && s.SpaceId == spaceId
                                          && s.GroupId == groupId,
                    ct);

            if (shiftRequest is null || originalSlot is null || requestedSlot is null)
                return NotFound();

            if (requestedSlot.SchedulingCycleId != shiftRequest.SchedulingCycleId)
                return Rejected("Requested shift must be in the same scheduling cycle.");

            if (!requestedSlot.HasAvailableCapacity())
                return Rejected("Requested shift is already full.");

            var hasDuplicateRequest = await _db.ShiftRequests
                .AsNoTracking()
                .AnyAsync(r => r.Id != shiftRequest.Id
                               && r.ShiftSlotId == requestedSlot.Id
                               && r.PersonId == changeRequest.PersonId
                               && (r.Status == ShiftRequestStatus.Pending || r.Status == ShiftRequestStatus.Approved),
                    ct);

            if (hasDuplicateRequest)
                return Rejected("Member already has an active request for the requested shift.");

            originalSlot.DecrementFillCount();
            requestedSlot.IncrementFillCount();
            shiftRequest.ReassignTo(changeRequest.PersonId, requestedSlot.Id);
        }

        try
        {
            changeRequest.Approve(CurrentUserId, req.AdminNote);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Rejected(ex.Message);
        }
    }

    [HttpPost("admin/{changeRequestId:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid spaceId,
        Guid groupId,
        Guid changeRequestId,
        [FromBody] ReviewShiftChangeRequest req,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        var changeRequest = await _db.ShiftChangeRequests
            .FirstOrDefaultAsync(r => r.Id == changeRequestId
                                      && r.SpaceId == spaceId
                                      && r.GroupId == groupId,
                ct);

        if (changeRequest is null)
            return NotFound();

        try
        {
            changeRequest.Reject(CurrentUserId, req.AdminNote);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Rejected(ex.Message);
        }
    }

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

    private IQueryable<ShiftChangeRequestRow> BuildChangeRequestQuery(Guid spaceId, Guid groupId)
    {
        var originalSlots = _db.ShiftSlots.AsNoTracking();
        var requestedSlots = _db.ShiftSlots.AsNoTracking();

        return _db.ShiftChangeRequests
            .AsNoTracking()
            .Where(r => r.SpaceId == spaceId && r.GroupId == groupId)
            .Join(_db.People.AsNoTracking(), r => r.PersonId, p => p.Id, (r, p) => new { Change = r, Person = p })
            .Join(originalSlots, rp => rp.Change.OriginalShiftSlotId, s => s.Id, (rp, s) => new { rp.Change, rp.Person, OriginalSlot = s })
            .Join(_db.GroupTasks.AsNoTracking(), rps => rps.OriginalSlot.GroupTaskId, t => t.Id, (rps, t) => new { rps.Change, rps.Person, rps.OriginalSlot, OriginalTask = t })
            .GroupJoin(requestedSlots, r => r.Change.RequestedShiftSlotId, s => s.Id, (r, requested) => new { r.Change, r.Person, r.OriginalSlot, r.OriginalTask, RequestedSlots = requested })
            .SelectMany(r => r.RequestedSlots.DefaultIfEmpty(), (r, requestedSlot) => new { r.Change, r.Person, r.OriginalSlot, r.OriginalTask, RequestedSlot = requestedSlot })
            .GroupJoin(_db.GroupTasks.AsNoTracking(), r => r.RequestedSlot == null ? Guid.Empty : r.RequestedSlot.GroupTaskId, t => t.Id, (r, requestedTasks) => new { r.Change, r.Person, r.OriginalSlot, r.OriginalTask, r.RequestedSlot, RequestedTasks = requestedTasks })
            .SelectMany(r => r.RequestedTasks.DefaultIfEmpty(), (r, requestedTask) => new ShiftChangeRequestRow(
                r.Change,
                r.Person.DisplayName ?? r.Person.FullName,
                r.OriginalSlot,
                r.OriginalTask.Name,
                r.RequestedSlot,
                requestedTask == null ? null : requestedTask.Name));
    }

    private static ShiftChangeRequestDto ToDto(ShiftChangeRequestRow row) =>
        new(
            row.Change.Id,
            row.Change.ShiftRequestId,
            row.Change.PersonId,
            row.PersonName,
            row.Change.OriginalShiftSlotId,
            row.OriginalSlot.Date,
            row.OriginalSlot.StartTime,
            row.OriginalSlot.EndTime,
            row.OriginalTaskName,
            row.Change.RequestedShiftSlotId,
            row.RequestedSlot?.Date,
            row.RequestedSlot?.StartTime,
            row.RequestedSlot?.EndTime,
            row.RequestedTaskName,
            row.Change.Reason,
            row.Change.Status.ToString(),
            row.Change.RequestedAt,
            row.Change.AdminNote,
            row.Change.ReviewedAt);

    private IActionResult Rejected(string detail) =>
        ProblemDetailsResults.Problem(
            HttpContext,
            statusCode: 422,
            title: "Unprocessable Entity",
            detail: detail,
            typeSlug: "shift-change-request-rejected");

    private sealed record ShiftChangeRequestRow(
        ShiftChangeRequest Change,
        string PersonName,
        ShiftSlot OriginalSlot,
        string OriginalTaskName,
        ShiftSlot? RequestedSlot,
        string? RequestedTaskName);
}

public record SubmitShiftChangeRequest(Guid ShiftRequestId, Guid? RequestedShiftSlotId, string Reason);

public record ReviewShiftChangeRequest(string? AdminNote);

public record ShiftChangeRequestDto(
    Guid Id,
    Guid ShiftRequestId,
    Guid PersonId,
    string PersonName,
    Guid OriginalShiftSlotId,
    DateOnly OriginalSlotDate,
    TimeOnly OriginalSlotStartTime,
    TimeOnly OriginalSlotEndTime,
    string OriginalTaskName,
    Guid? RequestedShiftSlotId,
    DateOnly? RequestedSlotDate,
    TimeOnly? RequestedSlotStartTime,
    TimeOnly? RequestedSlotEndTime,
    string? RequestedTaskName,
    string Reason,
    string Status,
    DateTime RequestedAt,
    string? AdminNote,
    DateTime? ReviewedAt);
