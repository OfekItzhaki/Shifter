using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Queries;

public record GetFreezePeriodChangesCountQuery(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId) : IRequest<FreezePeriodChangesCountResult>;

public record FreezePeriodChangesCountResult(
    int OverrideCount,
    int ManualAssignmentCount,
    int SwapCount,
    int TotalCount);

public class GetFreezePeriodChangesCountQueryHandler
    : IRequestHandler<GetFreezePeriodChangesCountQuery, FreezePeriodChangesCountResult>
{
    private readonly AppDbContext _db;

    public GetFreezePeriodChangesCountQueryHandler(AppDbContext db) => _db = db;

    public async Task<FreezePeriodChangesCountResult> Handle(
        GetFreezePeriodChangesCountQuery req, CancellationToken ct)
    {
        // Verify the group exists in the space
        var groupExists = await _db.Groups.AsNoTracking()
            .AnyAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId && g.DeletedAt == null, ct);

        if (!groupExists)
            throw new KeyNotFoundException("Group not found.");

        // Load the HomeLeaveConfig for the group to get FreezeStartedAt
        var config = await _db.HomeLeaveConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == req.SpaceId && c.GroupId == req.GroupId, ct);

        // Return zeros if freeze is not active or FreezeStartedAt is null
        if (config is null || !config.EmergencyFreezeActive || config.FreezeStartedAt is null)
        {
            return new FreezePeriodChangesCountResult(0, 0, 0, 0);
        }

        var freezeStartedAt = config.FreezeStartedAt.Value;

        // Get draft version IDs for this space
        var draftVersionIds = await _db.ScheduleVersions.AsNoTracking()
            .Where(v => v.SpaceId == req.SpaceId && v.Status == ScheduleVersionStatus.Draft)
            .Select(v => v.Id)
            .ToListAsync(ct);

        if (draftVersionIds.Count == 0)
        {
            return new FreezePeriodChangesCountResult(0, 0, 0, 0);
        }

        // Query all override assignments created during the freeze period in draft versions
        var freezeOverrides = await _db.Assignments.AsNoTracking()
            .Where(a => a.SpaceId == req.SpaceId
                && draftVersionIds.Contains(a.ScheduleVersionId)
                && a.Source == AssignmentSource.Override
                && a.CreatedAt >= freezeStartedAt)
            .Select(a => new { a.Id, a.TaskSlotId, a.ScheduleVersionId, a.ChangeReasonSummary })
            .ToListAsync(ct);

        if (freezeOverrides.Count == 0)
        {
            return new FreezePeriodChangesCountResult(0, 0, 0, 0);
        }

        // Identify swaps: slots that have 2+ override assignments in the same version
        // during the freeze period (paired reassignments where one person replaces another)
        var slotGroupCounts = freezeOverrides
            .GroupBy(a => new { a.ScheduleVersionId, a.TaskSlotId })
            .Where(g => g.Count() >= 2)
            .ToList();

        // Each swap involves a pair of overrides on the same slot
        var swapAssignmentIds = slotGroupCounts
            .SelectMany(g => g.Select(a => a.Id))
            .ToHashSet();

        var swapCount = slotGroupCounts.Count;

        // Manual assignments: overrides with a ChangeReasonSummary indicating manual action
        // that are NOT part of a swap (single override on a slot)
        var nonSwapOverrides = freezeOverrides
            .Where(a => !swapAssignmentIds.Contains(a.Id))
            .ToList();

        var manualAssignmentCount = nonSwapOverrides
            .Count(a => a.ChangeReasonSummary != null
                && a.ChangeReasonSummary.Contains("Manual override"));

        // Pure overrides: remaining non-swap overrides without manual assignment markers
        var overrideCount = nonSwapOverrides.Count - manualAssignmentCount;

        var totalCount = overrideCount + manualAssignmentCount + swapCount;

        return new FreezePeriodChangesCountResult(
            overrideCount,
            manualAssignmentCount,
            swapCount,
            totalCount);
    }
}
