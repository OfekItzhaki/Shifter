using FluentValidation;

namespace Jobuler.Application.Scheduling.Models;

/// <summary>
/// Represents a single constraint override in the sandbox publish request.
/// Action determines whether the constraint is being added, edited, or removed.
/// </summary>
public record ConstraintOverrideDto(
    string Action,
    Guid? ExistingConstraintId,
    string? RuleType,
    string? Severity,
    string? ScopeType,
    Guid? ScopeId,
    Dictionary<string, object>? Payload);

public class ConstraintOverrideDtoValidator : AbstractValidator<ConstraintOverrideDto>
{
    private static readonly string[] ValidActions = ["add", "edit", "remove"];
    private static readonly string[] ValidSeverities = ["hard", "soft"];

    public ConstraintOverrideDtoValidator()
    {
        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(a => ValidActions.Contains(a.ToLowerInvariant()))
            .WithMessage("Action must be one of: add, edit, remove.");

        // For "edit" and "remove", ExistingConstraintId is required
        RuleFor(x => x.ExistingConstraintId)
            .NotEmpty()
            .When(x => !string.IsNullOrEmpty(x.Action) && x.Action.ToLowerInvariant() != "add")
            .WithMessage("ExistingConstraintId is required for edit and remove actions.");

        // For "add", constraint fields are required
        When(x => !string.IsNullOrEmpty(x.Action) && x.Action.ToLowerInvariant() == "add", () =>
        {
            RuleFor(x => x.RuleType).NotEmpty().WithMessage("RuleType is required for add action.");
            RuleFor(x => x.Severity).NotEmpty().WithMessage("Severity is required for add action.");
            RuleFor(x => x.ScopeType).NotEmpty().WithMessage("ScopeType is required for add action.");
        });

        // When provided, validate severity value
        RuleFor(x => x.Severity)
            .Must(s => ValidSeverities.Contains(s!.ToLowerInvariant()))
            .When(x => !string.IsNullOrEmpty(x.Severity))
            .WithMessage("Severity must be one of: hard, soft.");
    }
}
