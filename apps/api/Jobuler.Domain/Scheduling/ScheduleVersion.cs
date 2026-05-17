using Jobuler.Domain.Common;

namespace Jobuler.Domain.Scheduling;

public enum ScheduleVersionStatus { Draft, Published, RolledBack, Archived, Discarded }

/// <summary>
/// Immutable snapshot of a schedule for a space.
/// Published versions are NEVER edited in place.
/// Rollback = create a new version with RollbackSourceVersionId set.
/// </summary>
public class ScheduleVersion : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public int VersionNumber { get; private set; }
    public ScheduleVersionStatus Status { get; private set; } = ScheduleVersionStatus.Draft;
    public Guid? BaselineVersionId { get; private set; }
    public Guid? SourceRunId { get; private set; }
    public Guid? RollbackSourceVersionId { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public string? SummaryJson { get; private set; }

    private ScheduleVersion() { }

    public static ScheduleVersion CreateDraft(
        Guid spaceId, int versionNumber, Guid? baselineVersionId,
        Guid? sourceRunId, Guid? createdByUserId, string? summaryJson = null) =>
        new()
        {
            SpaceId = spaceId,
            VersionNumber = versionNumber,
            Status = ScheduleVersionStatus.Draft,
            BaselineVersionId = baselineVersionId,
            SourceRunId = sourceRunId,
            CreatedByUserId = createdByUserId,
            SummaryJson = summaryJson
        };

    public static ScheduleVersion CreateRollback(
        Guid spaceId, int versionNumber, Guid rollbackSourceVersionId,
        Guid publishedByUserId) =>
        new()
        {
            SpaceId = spaceId,
            VersionNumber = versionNumber,
            Status = ScheduleVersionStatus.Draft,
            RollbackSourceVersionId = rollbackSourceVersionId,
            CreatedByUserId = publishedByUserId
        };

    public void Publish(Guid publishedByUserId)
    {
        if (Status != ScheduleVersionStatus.Draft)
            throw new InvalidOperationException("Only draft versions can be published.");

        Status = ScheduleVersionStatus.Published;
        PublishedByUserId = publishedByUserId;
        PublishedAt = DateTime.UtcNow;
    }

    public void MarkRolledBack() => Status = ScheduleVersionStatus.RolledBack;
    public void Archive()        => Status = ScheduleVersionStatus.Archived;

    public void Discard()
    {
        if (Status != ScheduleVersionStatus.Draft)
            throw new InvalidOperationException("Only draft versions can be discarded.");
        Status = ScheduleVersionStatus.Discarded;
    }
}
