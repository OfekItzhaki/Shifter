using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<AdminAssignShiftCommandHandler> _logger;

    public AdminAssignShiftCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        ILogger<AdminAssignShiftCommandHandler> logger)
    {
        _db = db;
        _permissions = permissions;
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

        _db.ShiftRequests.Add(shiftRequest);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Admin {AdminUserId} assigned person {PersonId} to slot {SlotId} (admin override). Request {RequestId}",
            request.RequestingUserId, request.PersonId, request.ShiftSlotId, shiftRequest.Id);

        return new AdminAssignShiftResult(
            Success: true,
            ShiftRequestId: shiftRequest.Id,
            ErrorMessage: null);
    }
}
