using FluentAssertions;
using Jobuler.Application.Scheduling.Commands;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.Scheduling;

public class CancelScheduleRunCommandTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Cancel_QueuedRun_MarksRunFailed()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var run = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Standard, null, userId);
        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync();

        var handler = new CancelScheduleRunCommandHandler(db);
        await handler.Handle(new CancelScheduleRunCommand(spaceId, run.Id, userId), CancellationToken.None);

        var cancelled = await db.ScheduleRuns.SingleAsync(r => r.Id == run.Id);
        cancelled.Status.Should().Be(ScheduleRunStatus.Failed);
        cancelled.ErrorSummary.Should().Be("Scheduler run cancelled by admin.");
        cancelled.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_RunWithDraft_DiscardsDraft()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var run = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Standard, null, userId);
        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync();

        var draft = ScheduleVersion.CreateDraft(spaceId, 1, null, run.Id, userId, null);
        db.ScheduleVersions.Add(draft);
        await db.SaveChangesAsync();

        var handler = new CancelScheduleRunCommandHandler(db);
        await handler.Handle(new CancelScheduleRunCommand(spaceId, run.Id, userId), CancellationToken.None);

        var cancelledDraft = await db.ScheduleVersions.SingleAsync(v => v.Id == draft.Id);
        cancelledDraft.Status.Should().Be(ScheduleVersionStatus.Discarded);
    }

    [Fact]
    public async Task Cancel_CompletedRun_DoesNotChangeStatus()
    {
        await using var db = CreateDb();
        var spaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var run = ScheduleRun.Create(spaceId, ScheduleRunTrigger.Standard, null, userId);
        run.MarkRunning("hash");
        run.MarkCompleted("{}");
        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync();

        var handler = new CancelScheduleRunCommandHandler(db);
        await handler.Handle(new CancelScheduleRunCommand(spaceId, run.Id, userId), CancellationToken.None);

        var completed = await db.ScheduleRuns.SingleAsync(r => r.Id == run.Id);
        completed.Status.Should().Be(ScheduleRunStatus.Completed);
    }
}
