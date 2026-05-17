using Jobuler.Application.Scheduling;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Jobuler.Infrastructure.Scheduling;

public class RedisSolverJobQueue : ISolverJobQueue
{
    private const string QueueKey = "jobuler:solver:jobs";

    // Case-insensitive so PascalCase JSON (from Serialize) maps to camelCase record constructor params
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisSolverJobQueue> _logger;

    public RedisSolverJobQueue(IConnectionMultiplexer redis, ILogger<RedisSolverJobQueue> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task EnqueueAsync(SolverJobMessage job, CancellationToken ct = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(job, JsonOptions);
            await db.ListRightPushAsync(QueueKey, json);
            _logger.LogInformation("Solver job enqueued: run_id={RunId} space_id={SpaceId}", job.RunId, job.SpaceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue solver job run_id={RunId}. Is Redis/Memurai running?", job.RunId);
            throw new InvalidOperationException("Queue service unavailable. Ensure Redis/Memurai is running.", ex);
        }
    }

    public async Task<SolverJobMessage?> DequeueAsync(CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        // Blocking pop with 2s timeout so the worker doesn't busy-spin
        var value = await db.ListLeftPopAsync(QueueKey);
        if (value.IsNullOrEmpty) return null;

        // Explicitly convert RedisValue to string before deserializing
        var json = (string?)value;
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<SolverJobMessage>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize solver job from Redis. Raw value: {Json}", json);
            // Don't re-throw — return null so the worker skips this malformed message
            return null;
        }
    }
}
