using FluentValidation;
using Jobuler.Application.Billing.Commands;

namespace Jobuler.Application.Billing.Validators;

public class UpgradeSpacePlanValidator : AbstractValidator<UpgradeSpacePlanCommand>
{
    public UpgradeSpacePlanValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.VariantId).NotEmpty();
    }
}
