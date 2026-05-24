using FluentValidation;
using Jobuler.Application.Billing.Commands;

namespace Jobuler.Application.Billing.Validators;

public class CreateSpaceCheckoutValidator : AbstractValidator<CreateSpaceCheckoutCommand>
{
    public CreateSpaceCheckoutValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
