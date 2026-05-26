using FluentValidation;
using Jobuler.Application.Scheduling.SelfService.Models;
using MediatR;

namespace Jobuler.Application.Scheduling.SelfService.Commands;

/// <summary>
/// Command to join the waitlist for a full shift slot.
/// Members can only join waitlists for slots they don't already have an active entry on.
/// </summary>
public record JoinWaitlistCommand(
    Guid SpaceId,
    Guid PersonId,
    Guid ShiftSlotId) : IRequest<WaitlistResult>;

public class JoinWaitlistCommandValidator : AbstractValidator<JoinWaitlistCommand>
{
    public JoinWaitlistCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty().WithMessage("SpaceId is required.");
        RuleFor(x => x.PersonId).NotEmpty().WithMessage("PersonId is required.");
        RuleFor(x => x.ShiftSlotId).NotEmpty().WithMessage("ShiftSlotId is required.");
    }
}

public class JoinWaitlistCommandHandler : IRequestHandler<JoinWaitlistCommand, WaitlistResult>
{
    private readonly IWaitlistService _waitlistService;

    public JoinWaitlistCommandHandler(IWaitlistService waitlistService)
    {
        _waitlistService = waitlistService;
    }

    public async Task<WaitlistResult> Handle(JoinWaitlistCommand request, CancellationToken ct)
    {
        return await _waitlistService.JoinWaitlistAsync(request.PersonId, request.ShiftSlotId, ct);
    }
}
