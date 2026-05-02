using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jobuler.Infrastructure.Scheduling;

public class SolverHttpClient : ISolverClient
{
    private readonly HttpClient _http;
    private readonly ILogger<SolverHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Do NOT use WhenWritingNull — Python/Pydantic requires Optional fields
        // to be explicitly present as null, not omitted entirely.
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public SolverHttpClient(HttpClient http, ILogger<SolverHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<SolverOutputDto> SolveAsync(SolverInputDto input, CancellationToken ct = default)
    {
        _logger.LogInformation("Calling solver: run_id={RunId} space_id={SpaceId} slots={Slots} people={People}",
            input.RunId, input.SpaceId, input.TaskSlots.Count, input.People.Count);

        // Ensure LockedSlotIds is never null — Python Pydantic requires a list, not null
        var safeInput = input with { LockedSlotIds = input.LockedSlotIds ?? [] };

        var response = await _http.PostAsJsonAsync("/solve", safeInput, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Solver returned {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
            // 504 = solver process timeout — treat as a timed-out run, not a crash
            if ((int)response.StatusCode == 504)
                throw new TimeoutException($"Solver process timed out: {body}");
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<SolverOutputDto>(JsonOptions, ct)
            ?? throw new InvalidOperationException("Solver returned empty response.");

        _logger.LogInformation("Solver response: run_id={RunId} feasible={Feasible} timed_out={TimedOut}",
            result.RunId, result.Feasible, result.TimedOut);

        return result;
    }
}
