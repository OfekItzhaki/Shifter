using FluentValidation;
using Jobuler.Application.Spaces.Commands;

namespace Jobuler.Application.Spaces.Validators;

public class LinkParentGroupCommandValidator : AbstractValidator<LinkParentGroupCommand>
{
    public LinkParentGroupCommandValidator()
    {
        RuleFor(x => x.SpaceId)
            .NotEmpty().WithMessage("Space ID is required.");

        RuleFor(x => x.ChildGroupId)
            .NotEmpty().WithMessage("Child group ID is required.");

        RuleFor(x => x.ParentGroupId)
            .NotEmpty().WithMessage("Parent group ID is required.");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty().WithMessage("Requesting user ID is required.");

        RuleFor(x => x)
            .Must(x => x.ChildGroupId != x.ParentGroupId)
            .WithMessage("A group cannot be its own parent.");
    }
}
