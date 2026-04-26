using Jobuler.Application.Scheduling;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Fallback solver job queue used when Redis is unavailable.
/// Logs the job but doesn't actually enqueue it — the solver won't run,
/// but the API won't crash with a 400 error.
/// </summary>
public class NoOpSolverJobQueue : ISolverJobQueue
{
    private readonly ILogger<NoOpSolverJobQueue> _logger;

    public NoOpSolverJobQueue(ILogger<NoOpSolverJobQueue> logger)
    {
        _logger = logger;
    }

    public Task EnqueueAsync(SolverJobMessage job, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "[NoOp Solver Queue] Redis unavailable — job NOT queued. RunId={RunId} SpaceId={SpaceId}. " +
            "Start Memurai/Redis to enable the solver.",
            job.RunId, job.SpaceId);
        return Task.CompletedTask;
    }

    public Task<SolverJobMessage?> DequeueAsync(CancellationToken ct = default)
        => Task.FromResult<SolverJobMessage?>(null);
}
