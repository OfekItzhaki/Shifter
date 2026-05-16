using FluentValidation;
using Jobuler.Application.Groups.Commands;
using Jobuler.Domain.Groups;

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

        RuleFor(x => x.TemplateType)
            .IsInEnum().WithMessage("Invalid template type. Must be one of: Army, Restaurant, Hospital, Security, Custom.");
    }
}

public class UpdateGroupSettingsCommandValidator : AbstractValidator<UpdateGroupSettingsCommand>
{
    public UpdateGroupSettingsCommandValidator()
    {
        RuleFor(x => x.SolverHorizonDays)
            .InclusiveBetween(1, 7).WithMessage("Solver horizon must be between 1 and 7 days.");

        // SolverStartDateTime is optional — null means "use now".
        // When provided, reject obviously wrong values:
        //   - More than 1 year in the past (likely a mistake)
        //   - More than 1 year in the future (solver horizon is max 90 days anyway)
        RuleFor(x => x.SolverStartDateTime)
            .Must(dt => dt == null || dt.Value >= DateTime.UtcNow.AddYears(-1))
            .WithMessage("Solver start date cannot be more than 1 year in the past.")
            .Must(dt => dt == null || dt.Value <= DateTime.UtcNow.AddYears(1))
            .WithMessage("Solver start date cannot be more than 1 year in the future.")
            .When(x => x.SolverStartDateTime.HasValue);
    }
}
