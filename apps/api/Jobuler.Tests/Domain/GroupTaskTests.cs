// Feature: admin-management-and-scheduling
// Unit tests for GroupTask domain entity

using FluentAssertions;
using Jobuler.Domain.Tasks;
using Xunit;

namespace Jobuler.Tests.Domain;

public class GroupTaskTests
{
    // ── Task 27.1: GroupTask.Create() produces correct field values ───────────

    [Fact]
    public void Create_WithValidInputs_SetsAllFields()
    {
        var spaceId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var startsAt = DateTime.UtcNow.AddDays(1);
        var endsAt = startsAt.AddHours(8);

        var task = GroupTask.Create(
            spaceId, groupId, "  שמירת לילה  ",
            startsAt, endsAt, 8, 2,
            TaskBurdenLevel.Disliked, false, true, userId);

        task.SpaceId.Should().Be(spaceId);
        task.GroupId.Should().Be(groupId);
        task.Name.Should().Be("שמירת לילה"); // trimmed
        task.StartsAt.Should().Be(startsAt);
        task.EndsAt.Should().Be(endsAt);
        task.ShiftDurationMinutes.Should().Be(8);
        task.RequiredHeadcount.Should().Be(2);
        task.BurdenLevel.Should().Be(TaskBurdenLevel.Disliked);
        task.AllowsDoubleShift.Should().BeFalse();
        task.AllowsOverlap.Should().BeTrue();
        task.IsActive.Should().BeTrue();
        task.CreatedByUserId.Should().Be(userId);
        task.UpdatedByUserId.Should().BeNull();
        task.Id.Should().NotBeEmpty();
        task.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("favorable", TaskBurdenLevel.Favorable)]
    [InlineData("neutral", TaskBurdenLevel.Neutral)]
    [InlineData("disliked", TaskBurdenLevel.Disliked)]
    [InlineData("hated", TaskBurdenLevel.Hated)]
    public void Create_WithAllBurdenLevels_SetsCorrectly(string _, TaskBurdenLevel level)
    {
        var task = GroupTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Task",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1),
            1, 1, level, false, false, Guid.NewGuid());

        task.BurdenLevel.Should().Be(level);
    }

    // ── Task 27.2: GroupTask.Deactivate() sets IsActive = false ──────────────

    [Fact]
    public void Deactivate_SetsIsActiveFalse_AndUpdatesUpdatedByUserId()
    {
        var task = GroupTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Task",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1),
            1, 1, TaskBurdenLevel.Neutral, false, false, Guid.NewGuid());

        var updaterId = Guid.NewGuid();
        task.Deactivate(updaterId);

        task.IsActive.Should().BeFalse();
        task.UpdatedByUserId.Should().Be(updaterId);
    }

    [Fact]
    public void Deactivate_IsIdempotent()
    {
        var task = GroupTask.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Task",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1),
            1, 1, TaskBurdenLevel.Neutral, false, false, Guid.NewGuid());

        var updaterId = Guid.NewGuid();
        task.Deactivate(updaterId);
        task.Deactivate(updaterId); // second call should not throw

        task.IsActive.Should().BeFalse();
    }

    // ── Task 27.3 & 27.4: ScheduleVersion.Discard() ──────────────────────────

    [Fact]
    public void Discard_FromDraft_SetsStatusToDiscarded()
    {
        var version = Jobuler.Domain.Scheduling.ScheduleVersion.CreateDraft(
            Guid.NewGuid(), 1, null, null, Guid.NewGuid());

        version.Discard();

        version.Status.Should().Be(Jobuler.Domain.Scheduling.ScheduleVersionStatus.Discarded);
    }

    [Fact]
    public void Discard_FromPublished_ThrowsInvalidOperation()
    {
        var version = Jobuler.Domain.Scheduling.ScheduleVersion.CreateDraft(
            Guid.NewGuid(), 1, null, null, Guid.NewGuid());
        version.Publish(Guid.NewGuid());

        var act = () => version.Discard();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*draft*");
    }

    [Fact]
    public void Discard_FromRolledBack_ThrowsInvalidOperation()
    {
        var version = Jobuler.Domain.Scheduling.ScheduleVersion.CreateDraft(
            Guid.NewGuid(), 1, null, null, Guid.NewGuid());
        version.Publish(Guid.NewGuid());
        version.MarkRolledBack();

        var act = () => version.Discard();

        act.Should().Throw<InvalidOperationException>();
    }

    // ── Task 27.5: GroupAlert.Update() trims whitespace ───────────────────────

    [Theory]
    [InlineData("  Title  ", "  Body  ", "warning")]
    [InlineData("Title", "Body", "info")]
    [InlineData("  A  ", "  B  ", "critical")]
    public void GroupAlertUpdate_TrimsWhitespace(string title, string body, string severity)
    {
        var alert = Jobuler.Domain.Groups.GroupAlert.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            "Original", "Original body",
            Jobuler.Domain.Groups.AlertSeverity.Info, Guid.NewGuid());

        Enum.TryParse<Jobuler.Domain.Groups.AlertSeverity>(severity, ignoreCase: true, out var sev);
        alert.Update(title, body, sev);

        alert.Title.Should().Be(title.Trim());
        alert.Body.Should().Be(body.Trim());
        alert.Severity.Should().Be(sev);
    }
}
