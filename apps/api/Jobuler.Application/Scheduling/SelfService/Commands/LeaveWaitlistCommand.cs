using FluentValidation;
using MediatR;

namespace Jobuler.Application.Scheduling.SelfService.Commands;

/// <summary>
/// Command to remove a member from the waitlist for a shift slot.
/// If the member has an active offer, treats removal as a decline and cascades to the next member.
/// </summary>
public record LeaveWaitlistCommand(
    Guid SpaceId,
    Guid PersonId,
    Guid ShiftSlotId) : IRequest;

public class LeaveWaitlistCommandValidator : AbstractValidator<LeaveWaitlistCommand>
{
    public LeaveWaitlistCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty().WithMessage("SpaceId is required.");
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.ShiftSlotId).NotEmpty().WithMessage("ShiftSlotId is required.");
    }
}

public class LeaveWaitlistCommandHandler : IRequestHandler<LeaveWaitlistCommand>
{
    private readonly IWaitlistService _waitlistService;

    public LeaveWaitlistCommandHandler(IWaitlistService waitlistService)
    {
        _waitlistService = waitlistService;
    }

    public async Task Handle(LeaveWaitlistCommand request, CancellationToken ct)
    {
        await _waitlistService.LeaveWaitlistAsync(request.PersonId, request.ShiftSlotId, ct);
    }
}
