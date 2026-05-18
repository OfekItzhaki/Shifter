using FluentValidation;
using Jobuler.Application.HomeLeave.Commands;

namespace Jobuler.Application.HomeLeave.Validators;

public class CancelHomeLeaveCommandValidator : AbstractValidator<CancelHomeLeaveCommand>
{
    public CancelHomeLeaveCommandValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason is not null)
            .WithMessage("Reason must not exceed 500 characters.");

        RuleFor(x => x.Confirmed)
            .Equal(true)
            .WithMessage("Recall must be explicitly confirmed.");
    }
}
