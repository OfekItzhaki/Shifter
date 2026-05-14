using FluentValidation;
using Jobuler.Application.Groups.Commands;

namespace Jobuler.Application.Groups.Validators;

public class CreateGroupRoleCommandValidator : AbstractValidator<CreateGroupRoleCommand>
{
    public CreateGroupRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(100).WithMessage("Role name must be 100 characters or fewer.");

        RuleFor(x => x.Color)
            .Matches(@"^#[0-9a-fA-F]{6}$")
            .When(x => x.Color != null)
            .WithMessage("Color must be a valid hex color (e.g., '#f59e0b')");
    }
}

public class UpdateGroupRoleCommandValidator : AbstractValidator<UpdateGroupRoleCommand>
{
    public UpdateGroupRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(100).WithMessage("Role name must be 100 characters or fewer.");

        RuleFor(x => x.Color)
            .Matches(@"^#[0-9a-fA-F]{6}$")
            .When(x => x.Color != null)
            .WithMessage("Color must be a valid hex color (e.g., '#f59e0b')");
    }
}
