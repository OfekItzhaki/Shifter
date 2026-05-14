using FluentAssertions;
using Jobuler.Domain.People;
using Xunit;

namespace Jobuler.Tests.Domain;

public class PresenceWindowTests
{
    [Fact]
    public void CreateManual_WithOnMission_ThrowsInvalidOperation()
    {
        var act = () => PresenceWindow.CreateManual(
            Guid.NewGuid(), Guid.NewGuid(),
            PresenceState.OnMission,
            DateTime.UtcNow, DateTime.UtcNow.AddHours(8));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*derived*");
    }

    [Fact]
    public void CreateDerived_SetsOnMissionState()
    {
        var window = PresenceWindow.CreateDerived(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow.AddHours(8));

        window.State.Should().Be(PresenceState.OnMission);
        window.IsDerived.Should().BeTrue();
    }

    [Fact]
    public void CreateManual_WithEndsBeforeStarts_ThrowsArgumentException()
    {
        var act = () => PresenceWindow.CreateManual(
            Guid.NewGuid(), Guid.NewGuid(),
            PresenceState.AtHome,
            DateTime.UtcNow.AddHours(8), DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Truncate_WithValidNewEndsAt_UpdatesEndsAt()
    {
        var startsAt = DateTime.UtcNow.AddHours(-2);
        var endsAt = DateTime.UtcNow.AddHours(6);
        var window = PresenceWindow.CreateDerivedAtHome(
            Guid.NewGuid(), Guid.NewGuid(), startsAt, endsAt);

        var truncateAt = DateTime.UtcNow;
        window.Truncate(truncateAt);

        window.EndsAt.Should().Be(truncateAt);
    }

    [Fact]
    public void Truncate_WithNewEndsAtBeforeStartsAt_ThrowsArgumentException()
    {
        var startsAt = DateTime.UtcNow.AddHours(-2);
        var endsAt = DateTime.UtcNow.AddHours(6);
        var window = PresenceWindow.CreateDerivedAtHome(
            Guid.NewGuid(), Guid.NewGuid(), startsAt, endsAt);

        var act = () => window.Truncate(startsAt.AddHours(-1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*after StartsAt*");
    }

    [Fact]
    public void Truncate_WithNewEndsAtAfterCurrentEndsAt_ThrowsArgumentException()
    {
        var startsAt = DateTime.UtcNow.AddHours(-2);
        var endsAt = DateTime.UtcNow.AddHours(6);
        var window = PresenceWindow.CreateDerivedAtHome(
            Guid.NewGuid(), Guid.NewGuid(), startsAt, endsAt);

        var act = () => window.Truncate(endsAt.AddHours(1));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*before current EndsAt*");
    }
}
