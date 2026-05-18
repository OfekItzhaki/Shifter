using FluentValidation;
using Jobuler.Application.Feedback.Commands;

namespace Jobuler.Application.Feedback.Validators;

public class SubmitFeedbackCommandValidator : AbstractValidator<SubmitFeedbackCommand>
{
    private static readonly string[] AllowedTypes = ["bug", "feedback"];

    public SubmitFeedbackCommandValidator()
    {
        RuleFor(x => x.Description)
            .Must(d => !string.IsNullOrWhiteSpace(d))
            .WithMessage("Description is required.")
            .Must(d => d != null && d.Trim().Length <= 5000)
            .WithMessage("Description must not exceed 5000 characters.");

        RuleFor(x => x.Type)
            .Must(t => AllowedTypes.Contains(t))
            .WithMessage("Type must be 'bug' or 'feedback'.");
    }
}
