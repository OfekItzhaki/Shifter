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
                ? "העוזר החכם עדיין לא מחובר למודל AI בסביבה הזו. אפשר עדיין לשלוח בקשת תמיכה לאופק, והיא תישלח למייל התמיכה לאחר הגדרת שליחת מיילים."
                : request.Locale == "ru"
                    ? "AI assistant is not configured in this environment yet. You can still send a human support request to Ofek after email delivery is configured."
                    : "The AI assistant is not connected to an AI model in this environment yet. You can still send a human support request to Ofek after email delivery is configured.",
            [
                new AiChatActionDto("contact", SupportLabel(request.Locale), BuildSupportPayload(request)),
                new AiChatActionDto("feedback", FeedbackLabel(request.Locale), null)
            ]));

    private static string SupportLabel(string locale) =>
        locale == "he" ? "דבר עם תמיכה" : locale == "ru" ? "Связаться с поддержкой" : "Contact support";

    private static string FeedbackLabel(string locale) =>
        locale == "he" ? "שלח פידבק" : locale == "ru" ? "Отправить отзыв" : "Send feedback";

    private static string BuildSupportPayload(AiChatRequestDto request) =>
        $"""
        Human support request

        User: {request.UserDisplayName ?? "unknown"}
        Current path: {request.CurrentPath ?? "unknown"}
        Admin mode: {request.IsAdminMode}

        Message:
        {request.Message}
        """;
}
