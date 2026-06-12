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
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OpenAiAssistant(HttpClient http, IConfiguration config, ILogger<OpenAiAssistant> logger)
    {
        _http = http;
        _logger = logger;
        _model = string.IsNullOrWhiteSpace(config["AI:Model"])
            ? "gpt-4o"
            : config["AI:Model"]!.Trim();
        _baseUrl = (config["AI:BaseUrl"] ?? "https://api.openai.com/v1").TrimEnd('/');

        var apiKey = config["AI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    public async Task<ParsedConstraintDto> ParseConstraintAsync(
        string naturalLanguageInput, string locale, CancellationToken ct = default)
    {
        var systemPrompt = locale == "he"
            ? "You are a scheduling assistant. The user enters an instruction in natural language. Convert it into a structured constraint."
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

    public async Task<string> ParseScheduleFileAsync(
        string fileContentBase64, string contentType, string fileName, CancellationToken ct = default)
    {
        var systemPrompt = """
            You are a military schedule parser. Extract all information from this schedule document.
            Return a JSON object with:
            {
              "people": [{"fullName": "...", "displayName": "..."}],
              "tasks": [{"name": "...", "shiftDurationMinutes": 240, "requiredHeadcount": 1, "burdenLevel": "neutral"}],
              "assignments": [{"personName": "...", "taskName": "...", "startsAt": "ISO datetime", "endsAt": "ISO datetime"}]
            }
            Extract Hebrew names as-is. Infer shift durations from the time slots shown.
            If a time range like "6-10" is shown, it means 06:00 to 10:00 (4 hours).
            If you can't determine exact dates, use today's date.
            Return ONLY valid JSON, no markdown fences or explanation.
            """;

        try
        {
            var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

            object[] messages;
            if (isImage)
            {
                // Use vision: send image as base64 data URL
                var dataUrl = $"data:{contentType};base64,{fileContentBase64}";
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Parse this schedule image and extract all people, tasks, and assignments." },
                            new { type = "image_url", image_url = new { url = dataUrl } }
                        }
                    }
                };
            }
            else
            {
                // For PDF/Excel: the content is already extracted as text
                var textContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(fileContentBase64));
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = $"Parse this schedule data and extract all people, tasks, and assignments:\n\n{textContent}" }
                };
            }

            var body = new
            {
                model = _model,
                messages,
                max_tokens = 4000,
                temperature = 0.1
            };

            var response = await _http.PostAsJsonAsync(
                $"{_baseUrl}/chat/completions", body, ct);
            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI schedule file parsing failed for: {FileName}", fileName);
            return string.Empty;
        }
    }

    public async Task<AiChatResponseDto> ChatAsync(
        AiChatRequestDto request, CancellationToken ct = default)
    {
        var lang = request.Locale == "he" ? "Hebrew" : request.Locale == "ru" ? "Russian" : "English";
        var recent = request.RecentMessages
            .TakeLast(8)
            .Select(m => $"{m.Role}: {m.Content}")
            .ToArray();

        var systemPrompt = $$"""
            You are Shifter's native support assistant.
            Answer in {{lang}}.
            Shifter is a smart shift scheduling product for teams. It supports automatic scheduling, self-service shift picking,
            group and space management, permissions/admin mode, constraints, schedule import/scan, notifications, billing, profiles,
            mobile/PWA usage, and feedback/bug reporting.

            Safety and authority rules:
            - You do not change schedules, users, billing, permissions, settings, or data.
            - If an action is needed, suggest a safe action for the app UI to show.
            - Do not claim that a human support agent has been notified unless the user explicitly uses a contact or feedback action.
            - Human support escalation is available through the contact action. It creates a prefilled support request for OfekLabs support.
            - If the user asks for legal, billing, security, or account-critical help, be concise and suggest contacting support.
            - If the user asks for human support, contact, escalation, a real person, or Ofek, include a contact action.
            - If the user is asking how to use Shifter, give practical step-by-step guidance.

            Return ONLY valid JSON in this exact shape:
            {
              "message": "short helpful answer",
              "suggestedActions": [
                { "type": "feedback|contact|open_path", "label": "button label", "payload": "optional path or contact target" }
              ]
            }
            Keep suggestedActions empty unless a concrete action helps.
            """;

        var userPrompt = $$"""
            User display name: {{request.UserDisplayName ?? "unknown"}}
            Current path: {{request.CurrentPath ?? "unknown"}}
            Authenticated: {{request.IsAuthenticated}}
            Admin mode: {{request.IsAdminMode}}

            Recent conversation:
            {{string.Join("\n", recent)}}

            User message:
            {{request.Message}}
            """;

        try
        {
            var response = await CallOpenAiAsync(systemPrompt, userPrompt, ct, maxTokens: 700);
            var parsed = JsonSerializer.Deserialize<AiChatRawResponse>(StripCodeFences(response), JsonOpts);

            var actions = (parsed?.SuggestedActions ?? [])
                .Where(a => a.Type is "feedback" or "contact" or "open_path")
                .Take(3)
                .Select(a => new AiChatActionDto(a.Type, a.Label, a.Payload))
                .ToList();
            AddSupportActionIfNeeded(request, actions);

            return new AiChatResponseDto(
                string.IsNullOrWhiteSpace(parsed?.Message)
                    ? DefaultChatFallback(request.Locale)
                    : parsed.Message.Trim(),
                actions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI chat failed for path {CurrentPath}", request.CurrentPath);
            return new AiChatResponseDto(DefaultChatFallback(request.Locale), [
                new AiChatActionDto("contact", SupportLabel(request.Locale), BuildSupportPayload(request)),
                new AiChatActionDto("feedback", FeedbackLabel(request.Locale), null)
            ]);
        }
    }

    private async Task<string> CallOpenAiAsync(
        string systemPrompt, string userPrompt, CancellationToken ct, int maxTokens = 500)
    {
        var body = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt }
            },
            max_tokens = maxTokens,
            temperature = 0.2  // low temperature for deterministic structured output
        };

        var response = await _http.PostAsJsonAsync(
            $"{_baseUrl}/chat/completions", body, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }

    private static string StripCodeFences(string value)
    {
        var json = value.Trim();
        if (!json.StartsWith("```", StringComparison.Ordinal)) return json;

        var firstNewline = json.IndexOf('\n');
        if (firstNewline > 0) json = json[(firstNewline + 1)..];
        if (json.EndsWith("```", StringComparison.Ordinal)) json = json[..^3];
        return json.Trim();
    }

    private static string DefaultChatFallback(string locale) =>
        locale == "he"
            ? "לא הצלחתי לענות כרגע. אפשר לנסות שוב או לשלוח פידבק לתמיכה."
            : locale == "ru"
                ? "I could not answer right now. Try again or send feedback to support."
                : "I could not answer right now. Try again or send feedback to support.";

    private static void AddSupportActionIfNeeded(
        AiChatRequestDto request,
        List<AiChatActionDto> actions)
    {
        if (!LooksLikeSupportEscalation(request.Message)) return;
        if (actions.Any(a => a.Type == "contact")) return;

        actions.Insert(0, new AiChatActionDto(
            "contact",
            SupportLabel(request.Locale),
            BuildSupportPayload(request)));
    }

    private static bool LooksLikeSupportEscalation(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        return normalized.Contains("support", StringComparison.Ordinal)
            || normalized.Contains("human", StringComparison.Ordinal)
            || normalized.Contains("contact", StringComparison.Ordinal)
            || normalized.Contains("ofek", StringComparison.Ordinal)
            || normalized.Contains("agent", StringComparison.Ordinal)
            || normalized.Contains("person", StringComparison.Ordinal)
            || normalized.Contains("תמיכה", StringComparison.Ordinal)
            || normalized.Contains("אופק", StringComparison.Ordinal)
            || normalized.Contains("נציג", StringComparison.Ordinal)
            || normalized.Contains("בן אדם", StringComparison.Ordinal);
    }

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

    private record ParsedConstraintResponse(
        bool Parsed,
        string? RuleType,
        string? ScopeType,
        string? ScopeHint,
        string? RulePayloadJson,
        string? ConfidenceNote);

    private record AiChatRawResponse(
        string Message,
        List<AiChatRawAction>? SuggestedActions);

    private record AiChatRawAction(
        string Type,
        string Label,
        string? Payload);
}
