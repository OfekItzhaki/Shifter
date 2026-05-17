using FluentValidation;

namespace Jobuler.Application.Scheduling.Models;

/// <summary>
/// Represents a single task override in the sandbox publish request.
/// Action determines whether the task is being added, edited, or removed.
/// </summary>
public record TaskOverrideDto(
    string Action,
    Guid? ExistingTaskId,
    string? Name,
    DateTime? StartsAt,
    DateTime? EndsAt,
    int? ShiftDurationMinutes,
    int? RequiredHeadcount,
    string? BurdenLevel,
    List<string>? RequiredQualificationNames);

public class TaskOverrideDtoValidator : AbstractValidator<TaskOverrideDto>
{
    private static readonly string[] ValidActions = ["add", "edit", "remove"];
    private static readonly string[] ValidBurdenLevels = ["easy", "normal", "hard"];

    public TaskOverrideDtoValidator()
    {
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => ValidActions.Contains(a.ToLowerInvariant()))
            .WithMessage("Action must be one of: add, edit, remove.");

        // For "edit" and "remove", ExistingTaskId is required
        RuleFor(x => x.ExistingTaskId)
            .NotEmpty()
            .When(x => !string.IsNullOrEmpty(x.Action) && x.Action.ToLowerInvariant() != "add")
            .WithMessage("ExistingTaskId is required for edit and remove actions.");

        // For "add", task fields are required
        When(x => !string.IsNullOrEmpty(x.Action) && x.Action.ToLowerInvariant() == "add", () =>
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required for add action.");
            RuleFor(x => x.StartsAt).NotNull().WithMessage("StartsAt is required for add action.");
            RuleFor(x => x.EndsAt).NotNull().WithMessage("EndsAt is required for add action.");
            RuleFor(x => x.RequiredHeadcount).NotNull().GreaterThanOrEqualTo(1)
                .WithMessage("RequiredHeadcount must be at least 1 for add action.");
        });

        // When provided, validate field values
        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt)
            .When(x => x.StartsAt.HasValue && x.EndsAt.HasValue)
            .WithMessage("EndsAt must be after StartsAt.");

        RuleFor(x => x.ShiftDurationMinutes)
            .GreaterThanOrEqualTo(1)
            .When(x => x.ShiftDurationMinutes.HasValue)
            .WithMessage("ShiftDurationMinutes must be at least 1.");

        RuleFor(x => x.RequiredHeadcount)
            .GreaterThanOrEqualTo(1)
            .When(x => x.RequiredHeadcount.HasValue)
            .WithMessage("RequiredHeadcount must be at least 1.");

        RuleFor(x => x.BurdenLevel)
            .Must(b => ValidBurdenLevels.Contains(b!.ToLowerInvariant()))
            .When(x => !string.IsNullOrEmpty(x.BurdenLevel))
            .WithMessage("BurdenLevel must be one of: easy, normal, hard.");
    }
}
