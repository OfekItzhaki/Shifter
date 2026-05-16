using FluentValidation;
using Jobuler.Application.Constraints.Commands;

namespace Jobuler.Application.Constraints.Validators;

public class CreateConstraintCommandValidator : AbstractValidator<CreateConstraintCommand>
{
    public CreateConstraintCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.RuleType)
            .NotEmpty()
            .MaximumLength(100);
        RuleFor(x => x.RulePayloadJson)
            .NotEmpty()
            .Must(BeValidJson).WithMessage("RulePayloadJson must be valid JSON.");
        RuleFor(x => x.EffectiveUntil)
            .Must((cmd, until) => until == null || cmd.EffectiveFrom == null || until >= cmd.EffectiveFrom)
            .WithMessage("EffectiveUntil must be on or after EffectiveFrom.");

        // max_task_type_per_period payload validation
        When(x => x.RuleType == "max_task_type_per_period", () =>
        {
            RuleFor(x => x.RulePayloadJson)
                .Must(HaveValidTaskTypeName).WithMessage("Payload must contain a non-empty 'task_type_name' string.")
                .Must(HavePositiveMax).WithMessage("Payload 'max' must be a positive integer.")
                .Must(HavePositivePeriodDays).WithMessage("Payload 'period_days' must be a positive integer.");
        });
    }

    private static bool BeValidJson(string json)
    {
        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool HaveValidTaskTypeName(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("task_type_name", out var prop))
                return prop.ValueKind == System.Text.Json.JsonValueKind.String && !string.IsNullOrWhiteSpace(prop.GetString());
            return false;
        }
        catch { return false; }
    }

    private static bool HavePositiveMax(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("max", out var prop) && prop.TryGetInt32(out var val))
                return val > 0;
            return false;
        }
        catch { return false; }
    }

    private static bool HavePositivePeriodDays(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("period_days", out var prop) && prop.TryGetInt32(out var val))
                return val > 0;
            return false;
        }
        catch { return false; }
    }
}
