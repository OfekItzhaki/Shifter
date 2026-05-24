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
            .WithMessage("*Only draft versions can be published*");
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

    [Fact]
    public void CreateRegenerationDraft_SetsAllFieldsCorrectly()
    {
        var spaceId = Guid.NewGuid();
        var versionNumber = 5;
        var sourceRunId = Guid.NewGuid();
        var supersedesVersionId = Guid.NewGuid();
        var createdByUserId = Guid.NewGuid();
        var summaryJson = """{"solver":"cp-sat","iterations":42}""";

        var version = ScheduleVersion.CreateRegenerationDraft(
            spaceId, versionNumber, sourceRunId,
            supersedesVersionId, createdByUserId, summaryJson);

        version.SpaceId.Should().Be(spaceId);
        version.VersionNumber.Should().Be(versionNumber);
        version.Status.Should().Be(ScheduleVersionStatus.Draft);
        version.SourceRunId.Should().Be(sourceRunId);
        version.SupersedesVersionId.Should().Be(supersedesVersionId);
        version.CreatedByUserId.Should().Be(createdByUserId);
        version.SourceType.Should().Be("regeneration");
        version.SummaryJson.Should().Be(summaryJson);
    }

    [Fact]
    public void CreateRegenerationDraft_WithoutSummaryJson_LeavesItNull()
    {
        var version = ScheduleVersion.CreateRegenerationDraft(
            Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        version.SummaryJson.Should().BeNull();
        version.SourceType.Should().Be("regeneration");
        version.Status.Should().Be(ScheduleVersionStatus.Draft);
    }

    [Fact]
    public void CreateRegenerationDraft_DoesNotSetRollbackOrBaselineFields()
    {
        var version = ScheduleVersion.CreateRegenerationDraft(
            Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        version.BaselineVersionId.Should().BeNull();
        version.RollbackSourceVersionId.Should().BeNull();
        version.PublishedByUserId.Should().BeNull();
        version.PublishedAt.Should().BeNull();
    }
}
