// Feature: admin-management-and-scheduling
// Property-based tests for ScheduleVersion discard (Task 32)

using FluentAssertions;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Application;

public class ScheduleVersionDiscardPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IPermissionService AllowAllPermissions()
    {
        var svc = Substitute.For<IPermissionService>();
        svc.RequirePermissionAsync(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return svc;
    }

    // ── Property 15: Create draft version → discard → status = Discarded ──────
    // Validates: Requirements 9.1, 9.2
    // Feature: admin-management-and-scheduling, Property 15: discard sets status to Discarded

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Property15_DiscardDraftVersion_StatusIsDiscarded(int versionNumber)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var version = ScheduleVersion.CreateDraft(spaceId, versionNumber, null, null, userId);
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();

        var handler = new DiscardVersionCommandHandler(db, AllowAllPermissions());

        // Act
        await handler.Handle(
            new DiscardVersionCommand(spaceId, version.Id, userId),
            CancellationToken.None);

        // Assert — status is Discarded
        var discarded = await db.ScheduleVersions.FindAsync(version.Id);
        discarded!.Status.Should().Be(ScheduleVersionStatus.Discarded);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Property15_DiscardDraftVersion_NotInDraftList(int versionNumber)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var version = ScheduleVersion.CreateDraft(spaceId, versionNumber, null, null, userId);
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();

        var handler = new DiscardVersionCommandHandler(db, AllowAllPermissions());

        // Act
        await handler.Handle(
            new DiscardVersionCommand(spaceId, version.Id, userId),
            CancellationToken.None);

        // Assert — not in draft list
        var drafts = await db.ScheduleVersions
            .Where(v => v.SpaceId == spaceId && v.Status == ScheduleVersionStatus.Draft)
            .ToListAsync();

        drafts.Should().BeEmpty();
        drafts.Should().NotContain(v => v.Id == version.Id);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Property15_DiscardPublishedVersion_ThrowsInvalidOperationException(int versionNumber)
    {
        // Arrange
        var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var version = ScheduleVersion.CreateDraft(spaceId, versionNumber, null, null, userId);
        version.Publish(userId); // make it Published
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();

        var handler = new DiscardVersionCommandHandler(db, AllowAllPermissions());

        // Act — try to discard a Published version
        var act = async () => await handler.Handle(
            new DiscardVersionCommand(spaceId, version.Id, userId),
            CancellationToken.None);

        // Assert — must throw InvalidOperationException
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*טיוטה*");

        // Version must still be Published
        var unchanged = await db.ScheduleVersions.FindAsync(version.Id);
        unchanged!.Status.Should().Be(ScheduleVersionStatus.Published);
    }
}
