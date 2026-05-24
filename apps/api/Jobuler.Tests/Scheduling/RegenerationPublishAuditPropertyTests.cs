// Feature: schedule-regeneration, Property 10: Audit log completeness on regeneration publish
// **Validates: Requirements 5.3**
//
// For any regeneration draft that is published, the audit log entry SHALL contain
// superseded version ID, regeneration run ID, and publishing user ID.

using System.Text.Json;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Scheduling;

/// <summary>
/// Input record for the regeneration publish audit log property test.
/// </summary>
public record RegenerationPublishAuditInput(
    Guid UserId,
    Guid SpaceId,
    Guid VersionId,
    Guid SupersedesVersionId,
    Guid RunId
)
{
    public override string ToString() =>
        $"UserId={UserId}, SpaceId={SpaceId}, VersionId={VersionId}, SupersedesVersionId={SupersedesVersionId}, RunId={RunId}";
}

/// <summary>
/// FsCheck arbitrary for generating valid RegenerationPublishAuditInput values.
/// </summary>
public static class RegenerationPublishAuditArbitraries
{
    public static Arbitrary<RegenerationPublishAuditInput> Generate()
    {
        var gen = from userId in Gen.Fresh(() => Guid.NewGuid())
                  from spaceId in Gen.Fresh(() => Guid.NewGuid())
                  from versionId in Gen.Fresh(() => Guid.NewGuid())
                  from supersedesVersionId in Gen.Fresh(() => Guid.NewGuid())
                  from runId in Gen.Fresh(() => Guid.NewGuid())
                  select new RegenerationPublishAuditInput(
                      userId, spaceId, versionId, supersedesVersionId, runId);

        return Arb.From(gen);
    }
}

/// <summary>
/// Property-based test verifying that publishing a regeneration draft always produces
/// an audit log entry containing the superseded version ID, regeneration run ID,
/// and publishing user ID.
/// </summary>
public class RegenerationPublishAuditPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedSpaceAsync(AppDbContext db, Guid spaceId)
    {
        var space = Space.Create("Test Space", Guid.NewGuid());
        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(space, spaceId);
        db.Spaces.Add(space);
        await db.SaveChangesAsync();
    }

    private static async Task<ScheduleVersion> SeedRegenerationDraftAsync(
        AppDbContext db, Guid spaceId, Guid versionId, Guid supersedesVersionId, Guid runId)
    {
        var version = ScheduleVersion.CreateRegenerationDraft(
            spaceId,
            versionNumber: 2,
            sourceRunId: runId,
            supersedesVersionId: supersedesVersionId,
            createdByUserId: Guid.NewGuid());

        typeof(Jobuler.Domain.Common.Entity).GetProperty("Id")!.SetValue(version, versionId);
        db.ScheduleVersions.Add(version);
        await db.SaveChangesAsync();
        return version;
    }

    // ── Property 10: Audit log completeness on regeneration publish ──────────

    [Property(MaxTest = 100, Arbitrary = new[] { typeof(RegenerationPublishAuditArbitraries) })]
    public bool Property10_RegenerationPublishAuditLog_ContainsAllRequiredFields(
        RegenerationPublishAuditInput input)
    {
        // Arrange
        var db = CreateDb();
        SeedSpaceAsync(db, input.SpaceId).GetAwaiter().GetResult();
        SeedRegenerationDraftAsync(db, input.SpaceId, input.VersionId, input.SupersedesVersionId, input.RunId)
            .GetAwaiter().GetResult();

        var audit = Substitute.For<IAuditLogger>();
        var config = Substitute.For<IConfiguration>();
        var logger = Substitute.For<ILogger<PublishVersionCommandHandler>>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var snapshotService = Substitute.For<IAssignmentSnapshotService>();
        var cumulativeTracker = Substitute.For<ICumulativeTracker>();
        var cache = Substitute.For<ICacheService>();

        snapshotService.CreateSnapshotsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SnapshotDiff(0, 0, 0, new List<AssignmentCountsDelta>())));
        cumulativeTracker.UpdateOnPublishAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        cache.RemoveByPatternAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Capture audit log call arguments
        string? capturedAfterJson = null;
        Guid? capturedActorUserId = null;
        string? capturedAction = null;
        string? capturedEntityType = null;
        Guid? capturedEntityId = null;

        audit.LogAsync(
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>()
        ).Returns(ci =>
        {
            capturedActorUserId = ci.ArgAt<Guid?>(1);
            capturedAction = ci.ArgAt<string>(2);
            capturedEntityType = ci.ArgAt<string?>(3);
            capturedEntityId = ci.ArgAt<Guid?>(4);
            capturedAfterJson = ci.ArgAt<string?>(6);
            return Task.CompletedTask;
        });

        var handler = new PublishVersionCommandHandler(
            db, audit, config, logger, scopeFactory,
            snapshotService, cumulativeTracker, cache);

        var command = new PublishVersionCommand(
            SpaceId: input.SpaceId,
            VersionId: input.VersionId,
            RequestingUserId: input.UserId);

        // Act
        handler.Handle(command, CancellationToken.None).GetAwaiter().GetResult();

        // Assert — audit log must have been called
        capturedAfterJson.Should().NotBeNullOrEmpty(
            "audit log afterJson must be present for regeneration publish");
        capturedAction.Should().Be("publish_schedule");
        capturedEntityType.Should().Be("schedule_version");
        capturedEntityId.Should().Be(input.VersionId);

        // Parse the afterJson and verify all required fields
        var afterDoc = JsonDocument.Parse(capturedAfterJson!);
        var root = afterDoc.RootElement;

        // supersedes_version_id must be present and match
        root.TryGetProperty("supersedes_version_id", out var supersedesProp).Should()
            .BeTrue("afterJson must contain 'supersedes_version_id'");
        supersedesProp.GetString().Should().Be(input.SupersedesVersionId.ToString(),
            "supersedes_version_id must match the version's SupersedesVersionId");

        // regeneration_run_id must be present and match
        root.TryGetProperty("regeneration_run_id", out var runIdProp).Should()
            .BeTrue("afterJson must contain 'regeneration_run_id'");
        runIdProp.GetString().Should().Be(input.RunId.ToString(),
            "regeneration_run_id must match the version's SourceRunId");

        // published_by_user_id must be present and match
        root.TryGetProperty("published_by_user_id", out var publishedByProp).Should()
            .BeTrue("afterJson must contain 'published_by_user_id'");
        publishedByProp.GetString().Should().Be(input.UserId.ToString(),
            "published_by_user_id must match the requesting user ID");

        return true;
    }
}
