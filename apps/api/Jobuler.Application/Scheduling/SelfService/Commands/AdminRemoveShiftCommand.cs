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
/// Admin command to remove a member from a shift slot.
/// Cancels the existing ShiftRequest with reason "admin_removed", decrements fill count,
/// and triggers waitlist processing if the slot drops below capacity.
/// Requires SchedulePublish permission.
/// </summary>
public record AdminRemoveShiftCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid ShiftSlotId,
    Guid PersonId,
    Guid RequestingUserId) : IRequest<AdminRemoveShiftResult>;

public record AdminRemoveShiftResult(
    bool Success,
    string? ErrorMessage);

public class AdminRemoveShiftCommandValidator : AbstractValidator<AdminRemoveShiftCommand>
{
    public AdminRemoveShiftCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty().WithMessage("SpaceId is required.");
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("GroupId is required.");
        RuleFor(x => x.ShiftSlotId).NotEmpty().WithMessage("ShiftSlotId is required.");
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.RequestingUserId).NotEmpty().WithMessage("RequestingUserId is required.");
    }
}

public class AdminRemoveShiftCommandHandler : IRequestHandler<AdminRemoveShiftCommand, AdminRemoveShiftResult>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IWaitlistService _waitlistService;
    private readonly ILogger<AdminRemoveShiftCommandHandler> _logger;

    public AdminRemoveShiftCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IWaitlistService waitlistService,
        ILogger<AdminRemoveShiftCommandHandler> logger)
    {
        _db = db;
        _permissions = permissions;
        _waitlistService = waitlistService;
        _logger = logger;
    }

    public async Task<AdminRemoveShiftResult> Handle(AdminRemoveShiftCommand request, CancellationToken ct)
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

        // Req 10.7: Find the member's active (Approved) request for this slot
        var shiftRequest = await _db.ShiftRequests
            .FirstOrDefaultAsync(r => r.ShiftSlotId == request.ShiftSlotId
                                      && r.PersonId == request.PersonId
                                      && r.Status == ShiftRequestStatus.Approved, ct);

        if (shiftRequest is null)
            throw new InvalidOperationException("No active assignment exists for this member on the specified slot.");

        // Req 10.4: Cancel the request with reason "admin_removed"
        shiftRequest.Cancel("admin_removed");

        // Decrement fill count
        slot.DecrementFillCount();

        await _db.SaveChangesAsync(ct);

        // Req 10.4: Trigger waitlist processing if slot now has capacity below required headcount
        if (slot.CurrentFillCount < slot.Capacity)
        {
            await _waitlistService.ProcessSlotReleasedAsync(slot.Id, ct);
        }

        _logger.LogInformation(
            "Admin {AdminUserId} removed person {PersonId} from slot {SlotId}. Request {RequestId} cancelled with reason 'admin_removed'.",
            request.RequestingUserId, request.PersonId, request.ShiftSlotId, shiftRequest.Id);

        return new AdminRemoveShiftResult(
            Success: true,
            ErrorMessage: null);
    }
}
