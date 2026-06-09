using Jobuler.Application.AI;

namespace Jobuler.Infrastructure.AI;

/// <summary>
/// No-op AI assistant used when AI:ApiKey is not configured.
/// Returns graceful fallback responses so the app runs without AI.
/// </summary>
public class NoOpAiAssistant : IAiAssistant
{
    public Task<ParsedConstraintDto> ParseConstraintAsync(
        string input, string locale, CancellationToken ct = default) =>
        Task.FromResult(new ParsedConstraintDto(
            false, null, null, null, null,
            "AI assistant is not configured. Please enter the constraint manually.",
            input));

    public Task<string> SummarizeDiffAsync(
        DiffContextDto diff, string locale, CancellationToken ct = default) =>
        Task.FromResult(
            $"Schedule updated: {diff.AddedCount} added, {diff.RemovedCount} removed, {diff.ChangedCount} changed.");

    public Task<string> ExplainInfeasibilityAsync(
        InfeasibilityContextDto context, string locale, CancellationToken ct = default) =>
        Task.FromResult(
            "The schedule could not be solved. Review the hard constraints and availability windows.");

    public Task<string> ParseScheduleFileAsync(
        string fileContentBase64, string contentType, string fileName, CancellationToken ct = default) =>
        Task.FromResult(string.Empty);

    public Task<AiChatResponseDto> ChatAsync(
        AiChatRequestDto request, CancellationToken ct = default) =>
        Task.FromResult(new AiChatResponseDto(
            request.Locale == "he"
                ? "העוזר החכם עדיין לא מוגדר. אפשר לשלוח פידבק או ליצור קשר עם התמיכה."
                : request.Locale == "ru"
                    ? "AI-assistant is not configured yet. You can send feedback or contact support."
                    : "The AI assistant is not configured yet. You can send feedback or contact support.",
            [
                new AiChatActionDto("feedback", request.Locale == "he" ? "שלח פידבק" : request.Locale == "ru" ? "Отправить отзыв" : "Send feedback", null)
            ]));
}
