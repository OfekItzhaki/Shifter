using FluentValidation;
using Jobuler.Application.Billing.Commands;

namespace Jobuler.Application.Billing.Validators;

public class RenewSpaceSubscriptionValidator : AbstractValidator<RenewSpaceSubscriptionCommand>
{
    public RenewSpaceSubscriptionValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
