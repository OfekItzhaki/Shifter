using FluentAssertions;
using Jobuler.Domain.Scheduling;
using Xunit;

namespace Jobuler.Tests.Domain;

public class ScheduleVersionTests
{
    [Fact]
    public void Publish_FromDraft_SetsStatusToPublished()
    {
        var version = ScheduleVersion.CreateDraft(
            Guid.NewGuid(), 1, null, null, Guid.NewGuid());

        version.Publish(Guid.NewGuid());

        version.Status.Should().Be(ScheduleVersionStatus.Published);
        version.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public void Publish_FromPublished_ThrowsInvalidOperation()
    {
        var version = ScheduleVersion.CreateDraft(
            Guid.NewGuid(), 1, null, null, Guid.NewGuid());
        version.Publish(Guid.NewGuid());

        var act = () => version.Publish(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*טיוטה*");
    }

    [Fact]
    public void CreateRollback_SetsRollbackSourceVersionId()
    {
        var spaceId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();

        var rollback = ScheduleVersion.CreateRollback(
            spaceId, 2, sourceId, Guid.NewGuid());

        rollback.RollbackSourceVersionId.Should().Be(sourceId);
        rollback.Status.Should().Be(ScheduleVersionStatus.Draft);
    }

    [Fact]
    public void MarkRolledBack_ChangesStatus()
    {
        var version = ScheduleVersion.CreateDraft(
            Guid.NewGuid(), 1, null, null, Guid.NewGuid());
        version.Publish(Guid.NewGuid());

        version.MarkRolledBack();

        version.Status.Should().Be(ScheduleVersionStatus.RolledBack);
    }
}
