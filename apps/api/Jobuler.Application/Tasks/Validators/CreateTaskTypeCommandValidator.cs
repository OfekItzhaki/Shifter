using FluentValidation;
using Jobuler.Application.Tasks.Commands;

namespace Jobuler.Application.Tasks.Validators;

public class CreateTaskTypeCommandValidator : AbstractValidator<CreateTaskTypeCommand>
{
    private static readonly string[] ValidBurdenLevels = ["Easy", "Normal", "Hard"];

    public CreateTaskTypeCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(x => x.Description)
            .MaximumLength(500).When(x => x.Description != null);
        RuleFor(x => x.DefaultPriority)
            .InclusiveBetween(1, 10).WithMessage("Priority must be between 1 and 10.");
    }
}

public class CreateTaskSlotCommandValidator : AbstractValidator<CreateTaskSlotCommand>
{
    public CreateTaskSlotCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.TaskTypeId).NotEmpty();
        RuleFor(x => x.StartsAt).NotEmpty();
        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt)
            .WithMessage("EndsAt must be after StartsAt.");
        RuleFor(x => x.RequiredHeadcount)
            .GreaterThan(0).WithMessage("Required headcount must be at least 1.")
            .LessThanOrEqualTo(100);
        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 10);
        RuleFor(x => x.Location)
            .MaximumLength(200).When(x => x.Location != null);
    }
}
