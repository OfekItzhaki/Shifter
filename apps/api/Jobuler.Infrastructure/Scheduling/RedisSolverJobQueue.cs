using Jobuler.Application.Scheduling;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Jobuler.Infrastructure.Scheduling;

public class RedisSolverJobQueue : ISolverJobQueue
{
    private const string QueueKey = "jobuler:solver:jobs";

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
            var json = JsonSerializer.Serialize(job);
            await db.ListRightPushAsync(QueueKey, json);
            _logger.LogInformation("Solver job enqueued: run_id={RunId} space_id={SpaceId}", job.RunId, job.SpaceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue solver job run_id={RunId}. Is Redis/Memurai running?", job.RunId);
            throw new InvalidOperationException("שירות התור אינו זמין. ודא ש-Memurai פועל.", ex);
        }
    }

    public async Task<SolverJobMessage?> DequeueAsync(CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        // Blocking pop with 2s timeout so the worker doesn't busy-spin
        var value = await db.ListLeftPopAsync(QueueKey);
        if (value.IsNullOrEmpty) return null;

        return JsonSerializer.Deserialize<SolverJobMessage>(value!);
    }
}
