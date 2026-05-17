namespace Jobuler.Application.Conflicts;

public interface IConflictDetectionService
{
    /// <summary>
    /// Detects conflicts for all persons with assignments in the given published version.
    /// Called after a ScheduleVersion is published.
    /// </summary>
    Task DetectOnPublishAsync(Guid spaceId, Guid versionId, CancellationToken ct = default);

    /// <summary>
    /// Detects conflicts for a specific user across all their linked person records.
    /// Called after successful login.
    /// </summary>
    Task DetectOnLoginAsync(Guid userId, CancellationToken ct = default);
}
