using FluentValidation;

namespace Jobuler.Application.Scheduling.Models;

/// <summary>
/// Request body for publishing sandbox overrides alongside a draft version.
/// All overrides are persisted in a single transaction before delegating to PublishVersionCommand.
/// </summary>
public record PublishSandboxRequest(
    Guid VersionId,
    List<TaskOverrideDto> TaskOverrides,
    List<ConstraintOverrideDto> ConstraintOverrides,
    List<Guid> MemberExclusions,
    SettingsOverrideDto? SettingsOverrides);

public class PublishSandboxRequestValidator : AbstractValidator<PublishSandboxRequest>
{
    public PublishSandboxRequestValidator()
    {
        RuleFor(x => x.VersionId).NotEmpty().WithMessage("VersionId is required.");

        RuleFor(x => x.TaskOverrides).NotNull().WithMessage("TaskOverrides is required.");
        RuleForEach(x => x.TaskOverrides).SetValidator(new TaskOverrideDtoValidator())
            .When(x => x.TaskOverrides != null);

        RuleFor(x => x.ConstraintOverrides).NotNull().WithMessage("ConstraintOverrides is required.");
        RuleForEach(x => x.ConstraintOverrides).SetValidator(new ConstraintOverrideDtoValidator())
            .When(x => x.ConstraintOverrides != null);

        RuleFor(x => x.MemberExclusions).NotNull().WithMessage("MemberExclusions is required.");

        RuleFor(x => x.SettingsOverrides).SetValidator(new SettingsOverrideDtoValidator()!)
            .When(x => x.SettingsOverrides != null);
    }
}
