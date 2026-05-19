using FluentValidation;
using Jobuler.Application.Billing.Commands;

namespace Jobuler.Application.Billing.Validators;

public class RenewSubscriptionValidator : AbstractValidator<RenewSubscriptionCommand>
{
    public RenewSubscriptionValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}
