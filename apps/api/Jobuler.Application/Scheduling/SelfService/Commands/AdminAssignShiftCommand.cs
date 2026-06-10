using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Application.Notifications;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jobuler.Application.Scheduling.SelfService.Commands;

/// <summary>
/// Admin command to assign a member to a shift slot, bypassing capacity and Max_Shifts constraints.
/// Creates a ShiftRequest with IsAdminOverride = true.
/// Requires SchedulePublish permission.
/// </summary>
public record AdminAssignShiftCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid ShiftSlotId,
    Guid PersonId,
    Guid RequestingUserId) : IRequest<AdminAssignShiftResult>;

public record AdminAssignShiftResult(
    bool Success,
    Guid? ShiftRequestId,
    string? ErrorMessage);

public class AdminAssignShiftCommandValidator : AbstractValidator<AdminAssignShiftCommand>
{
    public AdminAssignShiftCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty().WithMessage("SpaceId is required.");
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("GroupId is required.");
        RuleFor(x => x.ShiftSlotId).NotEmpty().WithMessage("ShiftSlotId is required.");
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.RequestingUserId).NotEmpty().WithMessage("RequestingUserId is required.");
    }
}

public class AdminAssignShiftCommandHandler : IRequestHandler<AdminAssignShiftCommand, AdminAssignShiftResult>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IPushNotificationSender _pushSender;
    private readonly ILogger<AdminAssignShiftCommandHandler> _logger;

    public AdminAssignShiftCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IPushNotificationSender pushSender,
        ILogger<AdminAssignShiftCommandHandler> logger)
    {
        _db = db;
        _permissions = permissions;
        _pushSender = pushSender;
        _logger = logger;
    }

    public async Task<AdminAssignShiftResult> Handle(AdminAssignShiftCommand request, CancellationToken ct)
    {
        // Req 10.5: Validate SchedulePublish permission
        await _permissions.RequirePermissionAsync(
            request.RequestingUserId, request.SpaceId, Permissions.SchedulePublish, ct);

        // Load the shift slot
        var slot = await _db.ShiftSlots
            .FirstOrDefaultAsync(s => s.Id == request.ShiftSlotId && s.SpaceId == request.SpaceId, ct);

        if (slot is null)
            throw new KeyNotFoundException("Shift slot not found.");

        // Validate the slot belongs to the specified group
        if (slot.GroupId != request.GroupId)
            throw new InvalidOperationException("The shift slot does not belong to the specified group.");

        // Req 10.8: Validate member belongs to the group
        var isMember = await _db.GroupMemberships
            .AnyAsync(gm => gm.GroupId == request.GroupId
                            && gm.PersonId == request.PersonId
                            && gm.SpaceId == request.SpaceId, ct);

        if (!isMember)
            throw new InvalidOperationException("The specified member does not belong to the group.");

        // Req 10.6: Check for duplicate assignment (Pending or Approved request on same slot by same person)
        var hasDuplicate = await _db.ShiftRequests
            .AnyAsync(r => r.ShiftSlotId == request.ShiftSlotId
                           && r.PersonId == request.PersonId
                           && (r.Status == ShiftRequestStatus.Pending || r.Status == ShiftRequestStatus.Approved), ct);

        if (hasDuplicate)
            throw new InvalidOperationException("The member is already assigned to this shift slot.");

        var waitlistEntry = await _db.WaitlistEntries
            .FirstOrDefaultAsync(e => e.SpaceId == request.SpaceId
                                      && e.ShiftSlotId == request.ShiftSlotId
                                      && e.PersonId == request.PersonId
                                      && (e.Status == WaitlistEntryStatus.Waiting || e.Status == WaitlistEntryStatus.Offered), ct);

        // Req 10.1, 10.2: Admin override bypasses capacity and Max_Shifts constraints
        // Create an approved ShiftRequest with admin override flag
        var shiftRequest = ShiftRequest.Create(
            spaceId: slot.SpaceId,
            shiftSlotId: slot.Id,
            personId: request.PersonId,
            groupId: slot.GroupId,
            schedulingCycleId: slot.SchedulingCycleId,
            isAdminOverride: true,
            processedByUserId: request.RequestingUserId);

        shiftRequest.Approve(request.RequestingUserId);

        // Req 10.3: Increment fill count (even beyond capacity for admin override)
        slot.IncrementFillCount();
        waitlistEntry?.Accept();

        _db.ShiftRequests.Add(shiftRequest);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminUserId} assigned person {PersonId} to slot {SlotId} (admin override). Request {RequestId}. Waitlist entry accepted: {AcceptedWaitlistEntry}",
            request.RequestingUserId, request.PersonId, request.ShiftSlotId, shiftRequest.Id, waitlistEntry is not null);

        await SendAdminAssignedNotificationAsync(request.PersonId, slot, shiftRequest.Id, ct);

        return new AdminAssignShiftResult(
            Success: true,
            ShiftRequestId: shiftRequest.Id,
            ErrorMessage: null);
    }

    private async Task SendAdminAssignedNotificationAsync(
        Guid personId,
        ShiftSlot slot,
        Guid shiftRequestId,
        CancellationToken ct)
    {
        try
        {
            var detail = await _db.People
                .AsNoTracking()
                .Where(p => p.Id == personId && p.SpaceId == slot.SpaceId && p.LinkedUserId != null)
                .Join(
                    _db.GroupTasks.AsNoTracking(),
                    p => slot.GroupTaskId,
                    t => t.Id,
                    (p, t) => new { p.LinkedUserId, TaskName = t.Name })
                .FirstOrDefaultAsync(ct);

            if (detail?.LinkedUserId is null)
                return;

            var title = "Shift Assigned";
            var body = $"An admin assigned you to {detail.TaskName} on {slot.Date:MMM dd} ({slot.StartTime:HH:mm}-{slot.EndTime:HH:mm}).";

            _db.Notifications.Add(Notification.Create(
                slot.SpaceId,
                detail.LinkedUserId.Value,
                "self_service.admin_assigned",
                title,
                body,
                JsonSerializer.Serialize(new
                {
                    shiftRequestId,
                    groupId = slot.GroupId,
                    shiftSlotId = slot.Id,
                    schedulingCycleId = slot.SchedulingCycleId,
                    date = slot.Date,
                    startTime = slot.StartTime.ToString("HH:mm"),
                    endTime = slot.EndTime.ToString("HH:mm"),
                    taskName = detail.TaskName
                })));

            await _db.SaveChangesAsync(ct);

            try
            {
                await _pushSender.SendPushToUserAsync(
                    detail.LinkedUserId.Value,
                    slot.SpaceId,
                    new PushPayload(title, body, "/favicon.jpeg", "/shifts"),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Push notification delivery failed for admin assignment (person {PersonId}, slot {SlotId}). In-app notification was persisted successfully.",
                    personId, slot.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send admin assignment notification for person {PersonId}, slot {SlotId}",
                personId, slot.Id);
        }
    }
}
