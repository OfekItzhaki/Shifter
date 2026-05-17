using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Commands;

public record RollbackVersionCommand(
    Guid SpaceId,
    Guid TargetVersionId,
    Guid RequestingUserId) : IRequest<Guid>;

public class RollbackVersionCommandHandler : IRequestHandler<RollbackVersionCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;
    private readonly ICumulativeTracker _cumulativeTracker;
    private readonly ICacheService _cache;

    public RollbackVersionCommandHandler(AppDbContext db, IAuditLogger audit, ICumulativeTracker cumulativeTracker, ICacheService cache)
    {
        _db = db;
        _audit = audit;
        _cumulativeTracker = cumulativeTracker;
        _cache = cache;
    }

    public async Task<Guid> Handle(RollbackVersionCommand req, CancellationToken ct)
    {
        var target = await _db.ScheduleVersions
            .FirstOrDefaultAsync(v =>
                v.Id == req.TargetVersionId &&
                v.SpaceId == req.SpaceId &&
                v.Status == ScheduleVersionStatus.Published, ct)
            ?? throw new KeyNotFoundException(
                "Target version not found or is not a published version.");

        var nextVersion = await _db.ScheduleVersions
            .Where(v => v.SpaceId == req.SpaceId)
            .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        nextVersion++;

        var rollbackVersion = ScheduleVersion.CreateRollback(
            req.SpaceId, nextVersion, req.TargetVersionId, req.RequestingUserId);

        _db.ScheduleVersions.Add(rollbackVersion);
        await _db.SaveChangesAsync(ct);

        var sourceAssignments = await _db.Assignments.AsNoTracking()
            .Where(a => a.ScheduleVersionId == req.TargetVersionId && a.SpaceId == req.SpaceId)
            .ToListAsync(ct);

        var newAssignments = sourceAssignments.Select(a => Assignment.Create(
            req.SpaceId, rollbackVersion.Id, a.TaskSlotId, a.PersonId,
            AssignmentSource.Solver, "Rollback from version " + target.VersionNumber))
            .ToList();

        _db.Assignments.AddRange(newAssignments);
        target.MarkRolledBack();
        await _db.SaveChangesAsync(ct);

        // Recompute cumulative hours for all affected persons from presence_windows
        var affectedPersonIds = sourceAssignments.Select(a => a.PersonId).Distinct().ToList();
        foreach (var personId in affectedPersonIds)
        {
            await _cumulativeTracker.RecomputeForPersonAsync(req.SpaceId, personId, ct);
        }

        // Invalidate cached schedule and status for all groups in this space
        await _cache.RemoveByPatternAsync($"schedule:{req.SpaceId}:*", ct);
        await _cache.RemoveByPatternAsync($"status:{req.SpaceId}:*", ct);

        await _audit.LogAsync(
            req.SpaceId, req.RequestingUserId,
            "rollback_schedule",
            "schedule_version", req.TargetVersionId,
            afterJson: $"{{\"new_version_id\":\"{rollbackVersion.Id}\"}}",
            ct: ct);

        return rollbackVersion.Id;
    }
}
