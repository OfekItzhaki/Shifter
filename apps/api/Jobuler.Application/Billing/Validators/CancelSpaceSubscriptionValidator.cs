using FluentValidation;
using Jobuler.Application.Billing.Commands;

namespace Jobuler.Application.Billing.Validators;

public class CancelSpaceSubscriptionValidator : AbstractValidator<CancelSpaceSubscriptionCommand>
{
    public CancelSpaceSubscriptionValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
