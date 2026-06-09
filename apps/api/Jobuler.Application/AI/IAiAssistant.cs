namespace Jobuler.Application.AI;

/// <summary>
/// AI assistant interface — optional helper layer only.
/// The AI never makes scheduling decisions. It only parses, summarizes, and explains.
/// All AI output must be reviewed and confirmed by an admin before being stored.
/// </summary>
public interface IAiAssistant
{
    /// <summary>
    /// Parse a natural language admin instruction into a structured candidate constraint.
    /// Example: "Ofek cannot do kitchen for 10 days" → ParsedConstraintDto
    /// The admin reviews and confirms before the constraint is saved.
    /// </summary>
    Task<ParsedConstraintDto> ParseConstraintAsync(
        string naturalLanguageInput,
        string locale,
        CancellationToken ct = default);

    /// <summary>
    /// Summarize a schedule diff in plain language for the admin review UI.
    /// </summary>
    Task<string> SummarizeDiffAsync(
        DiffContextDto diff,
        string locale,
        CancellationToken ct = default);

    /// <summary>
    /// Explain likely reasons for an infeasible solver result in plain language.
    /// </summary>
    Task<string> ExplainInfeasibilityAsync(
        InfeasibilityContextDto context,
        string locale,
        CancellationToken ct = default);

    /// <summary>
    /// Parse a schedule file (image, PDF, or extracted Excel text) using AI vision/text.
    /// Returns raw JSON string with people, tasks, and assignments.
    /// </summary>
    Task<string> ParseScheduleFileAsync(
        string fileContentBase64,
        string contentType,
        string fileName,
        CancellationToken ct = default);

    /// <summary>
    /// Answer a user-facing support/product question.
    /// The assistant may explain Shifter workflows and suggest next actions, but it never mutates data.
    /// </summary>
    Task<AiChatResponseDto> ChatAsync(
        AiChatRequestDto request,
        CancellationToken ct = default);
}

public record ParsedConstraintDto(
    bool Parsed,
    string? RuleType,
    string? ScopeType,
    string? ScopeHint,       // person name, role name, etc. — admin must confirm the ID
    string? RulePayloadJson,
    string? ConfidenceNote,  // human-readable note about confidence / ambiguity
    string RawInput);

public record DiffContextDto(
    int AddedCount,
    int RemovedCount,
    int ChangedCount,
    double StabilityPenalty,
    List<string> ExplanationFragments,
    bool TimedOut,
    bool Feasible);

public record InfeasibilityContextDto(
    List<string> HardConflictDescriptions,
    List<string> AffectedPeople,
    List<string> AffectedSlots);

public record AiChatRequestDto(
    string Message,
    string Locale,
    string? UserDisplayName,
    string? CurrentPath,
    bool IsAuthenticated,
    bool IsAdminMode,
    IReadOnlyList<AiChatMessageDto> RecentMessages);

public record AiChatMessageDto(
    string Role,
    string Content);

public record AiChatResponseDto(
    string Message,
    IReadOnlyList<AiChatActionDto> SuggestedActions);

public record AiChatActionDto(
    string Type,
    string Label,
    string? Payload);
