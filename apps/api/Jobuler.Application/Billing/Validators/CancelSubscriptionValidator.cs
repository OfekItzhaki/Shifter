using FluentValidation;
using Jobuler.Application.Billing.Commands;

namespace Jobuler.Application.Billing.Validators;

public class CancelSubscriptionValidator : AbstractValidator<CancelSubscriptionCommand>
{
    public CancelSubscriptionValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.ActorUserId).NotEmpty();
    }
}
