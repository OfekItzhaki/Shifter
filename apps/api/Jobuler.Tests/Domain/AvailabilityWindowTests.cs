using FluentAssertions;
using Jobuler.Domain.People;
using Xunit;

namespace Jobuler.Tests.Domain;

public class AvailabilityWindowTests
{
    [Fact]
    public void Create_WithValidRange_Succeeds()
    {
        var start = DateTime.UtcNow;
        var end   = start.AddHours(8);

        var window = AvailabilityWindow.Create(
            Guid.NewGuid(), Guid.NewGuid(), start, end, "Morning shift");

        window.StartsAt.Should().Be(start);
        window.EndsAt.Should().Be(end);
        window.Note.Should().Be("Morning shift");
    }

    [Fact]
    public void Create_WithEndsBeforeStarts_ThrowsArgumentException()
    {
        var start = DateTime.UtcNow;
        var end   = start.AddHours(-1);

        var act = () => AvailabilityWindow.Create(
            Guid.NewGuid(), Guid.NewGuid(), start, end);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEndsEqualToStarts_ThrowsArgumentException()
    {
        var now = DateTime.UtcNow;

        var act = () => AvailabilityWindow.Create(
            Guid.NewGuid(), Guid.NewGuid(), now, now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNullNote_SetsNoteToNull()
    {
        var window = AvailabilityWindow.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow.AddHours(4));

        window.Note.Should().BeNull();
    }

    [Fact]
    public void Create_TrimsWhitespaceFromNote()
    {
        var window = AvailabilityWindow.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            DateTime.UtcNow, DateTime.UtcNow.AddHours(4), "  note  ");

        window.Note.Should().Be("note");
    }
}
