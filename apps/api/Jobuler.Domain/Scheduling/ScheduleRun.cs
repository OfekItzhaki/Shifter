using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

public enum ScheduleRunTrigger { Standard, Emergency, Manual, Rollback }
public enum ScheduleRunStatus  { Queued, Running, Completed, Failed, TimedOut }

public class ScheduleRun : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public ScheduleRunTrigger TriggerType { get; private set; }
    public Guid? BaselineVersionId { get; private set; }
    public Guid? RequestedByUserId { get; private set; }
    public ScheduleRunStatus Status { get; private set; } = ScheduleRunStatus.Queued;
    public string? SolverInputHash { get; private set; }   // SHA-256 for deduplication
    public string? ProgressPhase { get; private set; }     // Current phase for live progress display
    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public int? DurationMs { get; private set; }
    public string? ResultSummaryJson { get; private set; }
    public string? ErrorSummary { get; private set; }

    private ScheduleRun() { }

    public static ScheduleRun Create(
        Guid spaceId, ScheduleRunTrigger trigger,
        Guid? baselineVersionId, Guid? requestedByUserId) =>
        new()
        {
            SpaceId = spaceId,
            TriggerType = trigger,
            BaselineVersionId = baselineVersionId,
            RequestedByUserId = requestedByUserId
        };

    public void SetProgressPhase(string phase)
    {
        ProgressPhase = phase;
    }

    public void MarkRunning(string inputHash)
    {
        Status = ScheduleRunStatus.Running;
        SolverInputHash = inputHash;
        StartedAt = DateTime.UtcNow;
        ProgressPhase = "solving";
    }

    public void MarkCompleted(string resultSummaryJson)
    {
        Status = ScheduleRunStatus.Completed;
        FinishedAt = DateTime.UtcNow;
        DurationMs = StartedAt.HasValue ? (int)(FinishedAt.Value - StartedAt.Value).TotalMilliseconds : null;
        ResultSummaryJson = resultSummaryJson;
        ProgressPhase = null;
    }

    public void MarkTimedOut(string resultSummaryJson)
    {
        Status = ScheduleRunStatus.TimedOut;
        FinishedAt = DateTime.UtcNow;
        DurationMs = StartedAt.HasValue ? (int)(FinishedAt.Value - StartedAt.Value).TotalMilliseconds : null;
        ResultSummaryJson = resultSummaryJson;
        ProgressPhase = null;
    }

    public void MarkFailed(string errorSummary)
    {
        Status = ScheduleRunStatus.Failed;
        FinishedAt = DateTime.UtcNow;
        DurationMs = StartedAt.HasValue ? (int)(FinishedAt.Value - StartedAt.Value).TotalMilliseconds : null;
        ErrorSummary = errorSummary;
        ProgressPhase = null;
    }
}
