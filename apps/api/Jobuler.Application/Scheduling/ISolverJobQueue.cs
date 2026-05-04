namespace Jobuler.Application.Scheduling;

public record SolverJobMessage(
    Guid RunId,
    Guid SpaceId,
    string TriggerMode,
    Guid? BaselineVersionId,
    Guid? RequestedByUserId,
    Guid? GroupId = null,
    DateTime? StartTime = null);

/// <summary>
/// Enqueues solver jobs via Redis. The background worker dequeues and processes them.
/// Jobs are idempotent — duplicate RunIds are ignored by the worker.
/// </summary>
public interface ISolverJobQueue
{
    Task EnqueueAsync(SolverJobMessage job, CancellationToken ct = default);
    Task<SolverJobMessage?> DequeueAsync(CancellationToken ct = default);
}
