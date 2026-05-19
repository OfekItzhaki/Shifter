using FluentValidation;
using Jobuler.Application.HomeLeave.Commands;

namespace Jobuler.Application.HomeLeave.Validators;

public class DeactivateFreezeWithDiscardCommandValidator : AbstractValidator<DeactivateFreezeWithDiscardCommand>
{
    public DeactivateFreezeWithDiscardCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();
    }
}
