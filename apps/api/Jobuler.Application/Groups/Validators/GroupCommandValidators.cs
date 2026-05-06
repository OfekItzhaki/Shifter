using FluentValidation;
using Jobuler.Application.Groups.Commands;

namespace Jobuler.Application.Groups.Validators;

public class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Group name is required.")
            .MaximumLength(100).WithMessage("Group name must be 100 characters or fewer.")
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Group name cannot be blank.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must be 500 characters or fewer.")
            .When(x => x.Description is not null);
    }
}

public class UpdateGroupSettingsCommandValidator : AbstractValidator<UpdateGroupSettingsCommand>
{
    public UpdateGroupSettingsCommandValidator()
    {
        RuleFor(x => x.SolverHorizonDays)
            .InclusiveBetween(1, 90).WithMessage("Solver horizon must be between 1 and 90 days.");
    }
}
