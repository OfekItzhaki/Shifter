using Jobuler.Api.Middleware;
using Jobuler.Application.Common;
using Jobuler.Application.Notifications;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Jobuler.Api.Controllers;

[ApiController]
[Route("spaces/{spaceId:guid}/groups/{groupId:guid}/shift-change-requests")]
[Authorize]
public class ShiftChangeRequestsController : ControllerBase
{
    private readonly IPermissionService _permissions;
    private readonly INotificationService _notificationService;
    private readonly IPushNotificationSender _pushSender;
    private readonly AppDbContext _db;

    public ShiftChangeRequestsController(
        IPermissionService permissions,
        INotificationService notificationService,
        IPushNotificationSender pushSender,
        AppDbContext db)
    {
        _permissions = permissions;
        _notificationService = notificationService;
        _pushSender = pushSender;
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

        var rows = await BuildChangeRequestQuery(spaceId, groupId, personId.Value).ToListAsync(ct);

        return Ok(rows
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

        var originalSlot = await _db.ShiftSlots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shiftRequest.ShiftSlotId
                                      && s.SpaceId == spaceId
                                      && s.GroupId == groupId,
                ct);

        if (originalSlot is null)
            return NotFound();

        if (HasShiftStarted(originalSlot))
            return Rejected("This shift has already started.");

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
            await NotifyAdminsChangeSubmittedAsync(changeRequest, ct);

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
            await NotifyAdminsChangeCancelledAsync(changeRequest, ct);
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

        ShiftChangeRequestStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ShiftChangeRequestStatus>(status, true, out var value))
                return BadRequest(new { error = "Invalid shift change request status." });

            parsedStatus = value;
        }

        var rows = await BuildChangeRequestQuery(spaceId, groupId, status: parsedStatus).ToListAsync(ct);

        return Ok(rows
            .OrderByDescending(r => r.Change.RequestedAt)
            .Select(r => ToDto(r))
            .ToList());
    }

    [HttpGet("admin/target-slots")]
    public async Task<IActionResult> ListTargetSlotsForAdmin(
        Guid spaceId,
        Guid groupId,
        [FromQuery] string cycleId,
        CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(CurrentUserId, spaceId, Permissions.ConstraintsManage, ct);

        Guid resolvedCycleId;
        if (string.Equals(cycleId, "current", StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTime.UtcNow;
            resolvedCycleId = await _db.SchedulingCycles
                .AsNoTracking()
                .Where(c => c.SpaceId == spaceId && c.GroupId == groupId && c.EndsAt >= now)
                .OrderBy(c => c.StartsAt < now ? 0 : 1)
                .ThenBy(c => c.StartsAt)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(ct);

            if (resolvedCycleId == Guid.Empty)
                return Ok(Array.Empty<object>());
        }
        else if (!Guid.TryParse(cycleId, out resolvedCycleId))
        {
            return BadRequest(new { error = "Invalid cycleId. Use a scheduling cycle id or 'current'." });
        }

        var slots = await _db.ShiftSlots
            .AsNoTracking()
            .Where(s => s.SpaceId == spaceId
                        && s.GroupId == groupId
                        && s.SchedulingCycleId == resolvedCycleId
                        && s.CurrentFillCount < s.Capacity)
            .Join(_db.GroupTasks.AsNoTracking(), s => s.GroupTaskId, t => t.Id, (s, t) => new
            {
                id = s.Id,
                shiftSlotId = s.Id,
                s.Date,
                s.StartTime,
                s.EndTime,
                taskName = t.Name,
                s.Capacity,
                s.CurrentFillCount,
                s.SchedulingCycleId
            })
            .OrderBy(s => s.Date)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        return Ok(slots);
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

        var targetShiftSlotId = req.TargetShiftSlotId ?? changeRequest.RequestedShiftSlotId;
        if (!targetShiftSlotId.HasValue)
            return Rejected("Choose a target shift before approving this change request.");

        if (req.TargetShiftSlotId.HasValue && req.TargetShiftSlotId.Value != changeRequest.RequestedShiftSlotId)
        {
            try
            {
                changeRequest.SetRequestedShiftSlot(req.TargetShiftSlotId.Value);
            }
            catch (InvalidOperationException ex)
            {
                return Rejected(ex.Message);
            }
        }

        if (targetShiftSlotId.HasValue)
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
                .FirstOrDefaultAsync(s => s.Id == targetShiftSlotId.Value
                                          && s.SpaceId == spaceId
                                          && s.GroupId == groupId,
                    ct);

            if (shiftRequest is null || originalSlot is null || requestedSlot is null)
                return NotFound();

            if (HasShiftStarted(originalSlot))
                return Rejected("This shift has already started.");

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
            await AddMemberReviewNotificationAsync(changeRequest, approved: true, ct);
            await _db.SaveChangesAsync(ct);
            await SendMemberReviewPushAsync(changeRequest, approved: true, ct);
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
            await AddMemberReviewNotificationAsync(changeRequest, approved: false, ct);
            await _db.SaveChangesAsync(ct);
            await SendMemberReviewPushAsync(changeRequest, approved: false, ct);
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

    private IQueryable<ShiftChangeRequestRow> BuildChangeRequestQuery(
        Guid spaceId,
        Guid groupId,
        Guid? personId = null,
        ShiftChangeRequestStatus? status = null)
    {
        var originalSlots = _db.ShiftSlots.AsNoTracking();
        var requestedSlots = _db.ShiftSlots.AsNoTracking();
        var changes = _db.ShiftChangeRequests
            .AsNoTracking()
            .Where(r => r.SpaceId == spaceId && r.GroupId == groupId);

        if (personId.HasValue)
            changes = changes.Where(r => r.PersonId == personId.Value);

        if (status.HasValue)
            changes = changes.Where(r => r.Status == status.Value);

        return changes
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

    private static bool HasShiftStarted(ShiftSlot slot) =>
        slot.Date.ToDateTime(slot.StartTime, DateTimeKind.Utc) <= DateTime.UtcNow;

    private async Task NotifyAdminsChangeSubmittedAsync(ShiftChangeRequest changeRequest, CancellationToken ct)
    {
        var detail = await GetChangeNotificationDetailAsync(changeRequest, ct);
        if (detail is null)
            return;

        await _notificationService.NotifySpaceAdminsAsync(
            changeRequest.SpaceId,
            "self_service.change_requested",
            "Shift Change Requested",
            $"{detail.PersonName} requested a change for {detail.OriginalTaskName} on {detail.OriginalSlot.Date:MMM dd}.",
            JsonSerializer.Serialize(new
            {
                changeRequestId = changeRequest.Id,
                shiftRequestId = changeRequest.ShiftRequestId,
                personId = changeRequest.PersonId,
                personName = detail.PersonName,
                groupId = changeRequest.GroupId,
                originalShiftSlotId = changeRequest.OriginalShiftSlotId,
                requestedShiftSlotId = changeRequest.RequestedShiftSlotId,
                originalTaskName = detail.OriginalTaskName,
                requestedTaskName = detail.RequestedTaskName,
                reason = changeRequest.Reason
            }),
            changeRequest.GroupId,
            ct);
    }

    private async Task NotifyAdminsChangeCancelledAsync(ShiftChangeRequest changeRequest, CancellationToken ct)
    {
        var detail = await GetChangeNotificationDetailAsync(changeRequest, ct);
        if (detail is null)
            return;

        await _notificationService.NotifySpaceAdminsAsync(
            changeRequest.SpaceId,
            "self_service.change_cancelled",
            "Shift Change Cancelled",
            $"{detail.PersonName} cancelled a shift change request for {detail.OriginalTaskName} on {detail.OriginalSlot.Date:MMM dd}.",
            JsonSerializer.Serialize(new
            {
                changeRequestId = changeRequest.Id,
                shiftRequestId = changeRequest.ShiftRequestId,
                personId = changeRequest.PersonId,
                personName = detail.PersonName,
                groupId = changeRequest.GroupId,
                originalShiftSlotId = changeRequest.OriginalShiftSlotId,
                requestedShiftSlotId = changeRequest.RequestedShiftSlotId
            }),
            changeRequest.GroupId,
            ct);
    }

    private async Task AddMemberReviewNotificationAsync(
        ShiftChangeRequest changeRequest,
        bool approved,
        CancellationToken ct)
    {
        var detail = await GetChangeNotificationDetailAsync(changeRequest, ct);
        if (detail?.LinkedUserId is null)
            return;

        var title = approved ? "Shift Change Approved" : "Shift Change Rejected";
        var body = approved
            ? $"Your change request for {detail.OriginalTaskName} on {detail.OriginalSlot.Date:MMM dd} was approved."
            : $"Your change request for {detail.OriginalTaskName} on {detail.OriginalSlot.Date:MMM dd} was rejected.";

        _db.Notifications.Add(Notification.Create(
            changeRequest.SpaceId,
            detail.LinkedUserId.Value,
            approved ? "self_service.change_approved" : "self_service.change_rejected",
            title,
            body,
            JsonSerializer.Serialize(new
            {
                changeRequestId = changeRequest.Id,
                shiftRequestId = changeRequest.ShiftRequestId,
                groupId = changeRequest.GroupId,
                originalShiftSlotId = changeRequest.OriginalShiftSlotId,
                requestedShiftSlotId = changeRequest.RequestedShiftSlotId,
                originalTaskName = detail.OriginalTaskName,
                requestedTaskName = detail.RequestedTaskName,
                adminNote = changeRequest.AdminNote,
                reviewedAt = changeRequest.ReviewedAt
            })));
    }

    private async Task SendMemberReviewPushAsync(
        ShiftChangeRequest changeRequest,
        bool approved,
        CancellationToken ct)
    {
        var detail = await GetChangeNotificationDetailAsync(changeRequest, ct);
        if (detail?.LinkedUserId is null)
            return;

        var title = approved ? "Shift Change Approved" : "Shift Change Rejected";
        var body = approved
            ? $"Your change request for {detail.OriginalTaskName} was approved."
            : $"Your change request for {detail.OriginalTaskName} was rejected.";

        try
        {
            await _pushSender.SendPushToUserAsync(
                detail.LinkedUserId.Value,
                changeRequest.SpaceId,
                new PushPayload(title, body, "/favicon.jpeg", "/shifts"),
                ct);
        }
        catch
        {
            // In-app notification is the source of truth; push failures must not affect review.
        }
    }

    private async Task<ChangeNotificationDetail?> GetChangeNotificationDetailAsync(
        ShiftChangeRequest changeRequest,
        CancellationToken ct)
    {
        var person = await _db.People
            .AsNoTracking()
            .Where(p => p.Id == changeRequest.PersonId && p.SpaceId == changeRequest.SpaceId)
            .Select(p => new { p.FullName, p.DisplayName, p.LinkedUserId })
            .FirstOrDefaultAsync(ct);

        var original = await _db.ShiftSlots
            .AsNoTracking()
            .Where(s => s.Id == changeRequest.OriginalShiftSlotId)
            .Join(_db.GroupTasks.AsNoTracking(), s => s.GroupTaskId, t => t.Id, (s, t) => new { Slot = s, TaskName = t.Name })
            .FirstOrDefaultAsync(ct);

        if (person is null || original is null)
            return null;

        string? requestedTaskName = null;
        if (changeRequest.RequestedShiftSlotId.HasValue)
        {
            requestedTaskName = await _db.ShiftSlots
                .AsNoTracking()
                .Where(s => s.Id == changeRequest.RequestedShiftSlotId.Value)
                .Join(_db.GroupTasks.AsNoTracking(), s => s.GroupTaskId, t => t.Id, (_, t) => t.Name)
                .FirstOrDefaultAsync(ct);
        }

        return new ChangeNotificationDetail(
            person.DisplayName ?? person.FullName,
            person.LinkedUserId,
            original.Slot,
            original.TaskName,
            requestedTaskName);
    }

    private sealed record ShiftChangeRequestRow(
        ShiftChangeRequest Change,
        string PersonName,
        ShiftSlot OriginalSlot,
        string OriginalTaskName,
        ShiftSlot? RequestedSlot,
        string? RequestedTaskName);

    private sealed record ChangeNotificationDetail(
        string PersonName,
        Guid? LinkedUserId,
        ShiftSlot OriginalSlot,
        string OriginalTaskName,
        string? RequestedTaskName);
}

public record SubmitShiftChangeRequest(Guid ShiftRequestId, Guid? RequestedShiftSlotId, string Reason);

public record ReviewShiftChangeRequest(string? AdminNote, Guid? TargetShiftSlotId = null);

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
