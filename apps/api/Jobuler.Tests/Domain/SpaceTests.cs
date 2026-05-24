using FluentAssertions;
using Jobuler.Domain.Spaces;
using Xunit;

namespace Jobuler.Tests.Domain;

public class SpaceTests
{
    [Fact]
    public void Create_WithValidData_SetsOwner()
    {
        var ownerId = Guid.NewGuid();
        var space = Space.Create("Test Space", ownerId, "desc", "he");

        space.OwnerUserId.Should().Be(ownerId);
        space.Name.Should().Be("Test Space");
        space.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        var act = () => Space.Create("", Guid.NewGuid());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TransferOwnership_UpdatesOwner()
    {
        var originalOwner = Guid.NewGuid();
        var newOwner = Guid.NewGuid();
        var space = Space.Create("Space", originalOwner);

        space.TransferOwnership(newOwner);

        space.OwnerUserId.Should().Be(newOwner);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var space = Space.Create("Space", Guid.NewGuid());
        space.Deactivate();
        space.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SoftDelete_SetsDeletedAtToUtcNow()
    {
        var space = Space.Create("Space", Guid.NewGuid());
        var before = DateTime.UtcNow;

        space.SoftDelete();

        space.DeletedAt.Should().NotBeNull();
        space.DeletedAt!.Value.Should().BeOnOrAfter(before);
        space.DeletedAt!.Value.Should().BeOnOrBefore(DateTime.UtcNow);
    }

    [Fact]
    public void Restore_ClearsDeletedAt()
    {
        var space = Space.Create("Space", Guid.NewGuid());
        space.SoftDelete();
        space.DeletedAt.Should().NotBeNull();

        space.Restore();

        space.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void ManagementTimeoutMinutes_DefaultsTo15()
    {
        var space = Space.Create("Space", Guid.NewGuid());
        space.ManagementTimeoutMinutes.Should().Be(15);
    }

    [Fact]
    public void SetManagementTimeout_WithValidValue_UpdatesTimeout()
    {
        var space = Space.Create("Space", Guid.NewGuid());

        space.SetManagementTimeout(30);

        space.ManagementTimeoutMinutes.Should().Be(30);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(120)]
    public void SetManagementTimeout_AtBoundaries_Succeeds(int minutes)
    {
        var space = Space.Create("Space", Guid.NewGuid());

        space.SetManagementTimeout(minutes);

        space.ManagementTimeoutMinutes.Should().Be(minutes);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(121)]
    [InlineData(200)]
    public void SetManagementTimeout_OutOfRange_ThrowsInvalidOperationException(int minutes)
    {
        var space = Space.Create("Space", Guid.NewGuid());

        var act = () => space.SetManagementTimeout(minutes);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Management timeout must be between 5 and 120 minutes.");
    }

    [Fact]
    public void DeletedAt_DefaultsToNull()
    {
        var space = Space.Create("Space", Guid.NewGuid());
        space.DeletedAt.Should().BeNull();
    }
}
