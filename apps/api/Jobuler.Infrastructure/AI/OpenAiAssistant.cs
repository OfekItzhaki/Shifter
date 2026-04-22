using Jobuler.Application.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Jobuler.Infrastructure.AI;

/// <summary>
/// OpenAI-backed implementation of IAiAssistant.
/// Uses GPT-4o with structured JSON output mode for constraint parsing.
/// All output is candidate data — never stored without admin confirmation.
/// </summary>
public class OpenAiAssistant : IAiAssistant
{
    private readonly HttpClient _http;
    private readonly ILogger<OpenAiAssistant> _logger;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OpenAiAssistant(HttpClient http, IConfiguration config, ILogger<OpenAiAssistant> logger)
    {
        _http = http;
        _logger = logger;
        _model = config["AI:Model"] ?? "gpt-4o";

        var apiKey = config["AI:ApiKey"]
            ?? throw new InvalidOperationException("AI:ApiKey not configured.");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<ParsedConstraintDto> ParseConstraintAsync(
        string naturalLanguageInput, string locale, CancellationToken ct = default)
    {
        var systemPrompt = locale == "he"
            ? "אתה עוזר לניהול לוח זמנים. המשתמש מזין הוראה בשפה טבעית. המר אותה לאילוץ מובנה."
            : "You are a scheduling assistant. Convert the user's natural language instruction into a structured constraint.";

        var userPrompt = $$"""
            Convert this instruction to a scheduling constraint JSON.
            Instruction: "{{naturalLanguageInput}}"

            Respond with JSON only, in this exact shape:
            {
              "parsed": true/false,
              "ruleType": "no_task_type_restriction|min_rest_hours|max_kitchen_per_week|...",
              "scopeType": "person|role|group|task_type|space",
              "scopeHint": "name or description of the target (not an ID)",
              "rulePayloadJson": "{...}",
              "confidenceNote": "brief note about confidence or ambiguity"
            }

            Known rule types: no_task_type_restriction, min_rest_hours, max_kitchen_per_week,
            no_consecutive_burden, min_base_headcount.
            If you cannot parse it, set parsed=false and explain in confidenceNote.
            """;

        try
        {
            var response = await CallOpenAiAsync(systemPrompt, userPrompt, ct);
            var parsed = JsonSerializer.Deserialize<ParsedConstraintResponse>(response, JsonOpts);

            return new ParsedConstraintDto(
                parsed?.Parsed ?? false,
                parsed?.RuleType,
                parsed?.ScopeType,
                parsed?.ScopeHint,
                parsed?.RulePayloadJson,
                parsed?.ConfidenceNote,
                naturalLanguageInput);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI constraint parsing failed for input: {Input}", naturalLanguageInput);
            return new ParsedConstraintDto(false, null, null, null, null,
                "AI parsing failed. Please enter the constraint manually.", naturalLanguageInput);
        }
    }

    public async Task<string> SummarizeDiffAsync(
        DiffContextDto diff, string locale, CancellationToken ct = default)
    {
        var lang = locale == "he" ? "Hebrew" : locale == "ru" ? "Russian" : "English";
        var prompt = $"""
            Summarize this schedule change in {lang} in 2-3 sentences for an admin.
            Added: {diff.AddedCount}, Removed: {diff.RemovedCount}, Changed: {diff.ChangedCount}
            Stability penalty: {diff.StabilityPenalty:F1}
            Feasible: {diff.Feasible}, Timed out: {diff.TimedOut}
            Notes: {string.Join("; ", diff.ExplanationFragments)}
            Be concise and factual. Do not invent details.
            """;

        try
        {
            return await CallOpenAiAsync(
                "You are a scheduling assistant. Summarize schedule changes clearly.",
                prompt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI diff summary failed.");
            return $"Schedule updated: {diff.AddedCount} added, {diff.RemovedCount} removed, {diff.ChangedCount} changed.";
        }
    }

    public async Task<string> ExplainInfeasibilityAsync(
        InfeasibilityContextDto context, string locale, CancellationToken ct = default)
    {
        var lang = locale == "he" ? "Hebrew" : locale == "ru" ? "Russian" : "English";
        var prompt = $"""
            Explain in {lang} why this schedule could not be solved.
            Hard conflicts: {string.Join("; ", context.HardConflictDescriptions)}
            Affected people: {string.Join(", ", context.AffectedPeople)}
            Affected slots: {string.Join(", ", context.AffectedSlots)}
            Suggest what an admin could change to resolve it. Be concise.
            """;

        try
        {
            return await CallOpenAiAsync(
                "You are a scheduling assistant. Explain scheduling conflicts clearly.",
                prompt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI infeasibility explanation failed.");
            return "The schedule could not be solved. Review the hard constraints and availability windows.";
        }
    }

    private async Task<string> CallOpenAiAsync(
        string systemPrompt, string userPrompt, CancellationToken ct)
    {
        var body = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt }
            },
            max_tokens = 500,
            temperature = 0.2  // low temperature for deterministic structured output
        };

        var response = await _http.PostAsJsonAsync(
            "https://api.openai.com/v1/chat/completions", body, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private record ParsedConstraintResponse(
        bool Parsed,
        string? RuleType,
        string? ScopeType,
        string? ScopeHint,
        string? RulePayloadJson,
        string? ConfidenceNote);
}
