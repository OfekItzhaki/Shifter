using Jobuler.Domain.Scheduling;

namespace Jobuler.Application.Scheduling;

/// <summary>
/// Creates and manages daily snapshots from published schedule versions.
/// Snapshots enable historical viewing and incremental statistics computation.
/// </summary>
public interface IAssignmentSnapshotService
{
    /// <summary>
    /// Creates daily_snapshot rows for all persons/days in the published version.
    /// Replaces future-dated overlapping snapshots; preserves past-dated ones.
    /// Returns the diff (added, replaced, preserved counts).
    /// </summary>
    Task<SnapshotDiff> CreateSnapshotsAsync(Guid spaceId, Guid versionId, CancellationToken ct);

    /// <summary>
    /// Retrieves historical assignments for a date range (for schedule viewing).
    /// Respects the group's schedule_history_retention_days setting.
    /// </summary>
    Task<List<DailySnapshotDto>> GetHistoricalAsync(
        Guid spaceId, Guid groupId, DateOnly startDate, DateOnly endDate, CancellationToken ct);
}

/// <summary>
/// DTO representing a single daily snapshot for API responses.
/// </summary>
public record DailySnapshotDto(
    Guid Id,
    Guid PersonId,
    Guid GroupId,
    DateOnly SnapshotDate,
    Guid? TaskTypeId,
    Guid? SlotId,
    DateTime? ShiftStart,
    DateTime? ShiftEnd,
    string? BurdenLevel,
    Guid VersionId,
    Guid PeriodId);
