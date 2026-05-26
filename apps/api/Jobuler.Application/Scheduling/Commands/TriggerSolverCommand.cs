using FluentValidation;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Commands;

public record TriggerSolverCommand(
    Guid SpaceId,
    string TriggerMode,        // standard | emergency
    Guid? RequestedByUserId,
    Guid? GroupId = null,
    DateTime? StartTime = null) : IRequest<Guid>;

public class TriggerSolverCommandValidator : FluentValidation.AbstractValidator<TriggerSolverCommand>
{
    private static readonly string[] ValidModes = ["standard", "emergency"];

    public TriggerSolverCommandValidator()
    {
        RuleFor(x => x.TriggerMode)
            .NotEmpty()
            .Must(m => ValidModes.Contains(m?.ToLowerInvariant()))
            .WithMessage("TriggerMode must be 'standard' or 'emergency'.");
    }
}

public class TriggerSolverCommandHandler : IRequestHandler<TriggerSolverCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ISolverJobQueue _queue;

    public TriggerSolverCommandHandler(AppDbContext db, ISolverJobQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task<Guid> Handle(TriggerSolverCommand request, CancellationToken ct)
    {
        // Set PostgreSQL session variable so RLS policies allow queries on this space.
        // Skipped when using an in-memory provider (e.g. unit tests).
        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_space_id', {0}, TRUE), set_config('app.current_user_id', {1}, TRUE)",
                request.SpaceId.ToString(),
                request.RequestedByUserId?.ToString() ?? "");
        }

        // ── Limited_Mode guard ────────────────────────────────────────────────
        if (request.GroupId.HasValue)
        {
            var group = await _db.Groups
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == request.GroupId.Value && g.SpaceId == request.SpaceId, ct);

            group?.EnsureActive();

            // ── SelfService guard ─────────────────────────────────────────────
            // Self-service groups bypass the solver entirely (Requirement 1.2).
            if (group?.SchedulingMode == SchedulingMode.SelfService)
            {
                throw new InvalidOperationException(
                    "Cannot trigger solver for a group in SelfService scheduling mode. " +
                    "Self-service groups manage shifts through the request-based flow.");
            }
        }

        // ── Stale-task guard ──────────────────────────────────────────────────
        // If a group is specified, reject the run when every active task for that
        // group ends before the effective horizon start (i.e. all tasks are in the
        // past). Scheduling against only past tasks produces an empty, meaningless
        // draft and wastes solver capacity.
        if (request.GroupId.HasValue)
        {
            var nowUtc = request.StartTime.HasValue
                ? DateTime.SpecifyKind(request.StartTime.Value, DateTimeKind.Utc)
                : DateTime.UtcNow;

            var hasActiveFutureTasks = await _db.GroupTasks
                .AsNoTracking()
                .AnyAsync(t =>
                    t.SpaceId == request.SpaceId &&
                    t.GroupId == request.GroupId.Value &&
                    t.IsActive &&
                    t.EndsAt > nowUtc, ct);

            if (!hasActiveFutureTasks)
            {
                throw new InvalidOperationException(
                    "Cannot create a schedule: all tasks for this group end in the past. " +
                    "Update the task dates before running the scheduler.");
            }
        }

        // Find the current published version to use as baseline
        var baseline = await _db.ScheduleVersions
            .AsNoTracking()
            .Where(v => v.SpaceId == request.SpaceId && v.Status == ScheduleVersionStatus.Published)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);

        // ── Auto-discard existing drafts ──────────────────────────────────────
        // When a new solver run is triggered, automatically discard any existing
        // Draft versions for this space. This prevents stale drafts from
        // accumulating and confusing the admin.
        var existingDrafts = await _db.ScheduleVersions
            .Where(v => v.SpaceId == request.SpaceId && v.Status == ScheduleVersionStatus.Draft)
            .ToListAsync(ct);

        foreach (var draft in existingDrafts)
            draft.Discard();

        var trigger = request.TriggerMode == "emergency"
            ? ScheduleRunTrigger.Emergency
            : ScheduleRunTrigger.Standard;

        var run = ScheduleRun.Create(
            request.SpaceId, trigger, baseline?.Id, request.RequestedByUserId);

        _db.ScheduleRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        // Enqueue — worker picks it up asynchronously
        await _queue.EnqueueAsync(new SolverJobMessage(
            run.Id, request.SpaceId, request.TriggerMode,
            baseline?.Id, request.RequestedByUserId, request.GroupId, request.StartTime), ct);

        return run.Id;
    }
}
