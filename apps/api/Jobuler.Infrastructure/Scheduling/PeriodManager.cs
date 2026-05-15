using Jobuler.Application.Scheduling;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Manages subscription period lifecycle — creation, closure, and querying.
/// On period open, resets cumulative counters for the group.
/// On period close, preserves all associated data intact.
/// </summary>
public class PeriodManager : IPeriodManager
{
    private readonly AppDbContext _db;
    private readonly ICumulativeTracker _cumulativeTracker;
    private readonly ILogger<PeriodManager> _logger;

    public PeriodManager(
        AppDbContext db,
        ICumulativeTracker cumulativeTracker,
        ILogger<PeriodManager> logger)
    {
        _db = db;
        _cumulativeTracker = cumulativeTracker;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> OpenPeriodAsync(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        // Close any existing active period for this group first
        var existingActive = await _db.SubscriptionPeriods
            .FirstOrDefaultAsync(sp =>
                sp.SpaceId == spaceId && sp.GroupId == groupId && sp.Status == "active", ct);

        if (existingActive != null)
        {
            existingActive.Close();
            _logger.LogInformation(
                "Closed existing active period {PeriodId} for group {GroupId} before opening new one.",
                existingActive.Id, groupId);
        }

        // Create a new period
        var newPeriod = SubscriptionPeriod.Create(spaceId, groupId);
        _db.SubscriptionPeriods.Add(newPeriod);
        await _db.SaveChangesAsync(ct);

        // Reset cumulative counters for the group, scoped to the new period
        await _cumulativeTracker.ResetPeriodCountersAsync(spaceId, groupId, newPeriod.Id, ct);

        _logger.LogInformation(
            "Opened new subscription period {PeriodId} for group {GroupId} in space {SpaceId}.",
            newPeriod.Id, groupId, spaceId);

        return newPeriod.Id;
    }

    /// <inheritdoc />
    public async Task ClosePeriodAsync(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        var activePeriod = await _db.SubscriptionPeriods
            .FirstOrDefaultAsync(sp =>
                sp.SpaceId == spaceId && sp.GroupId == groupId && sp.Status == "active", ct);

        if (activePeriod == null)
        {
            _logger.LogWarning(
                "No active period found for group {GroupId} in space {SpaceId}. Nothing to close.",
                groupId, spaceId);
            return;
        }

        activePeriod.Close();
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Closed subscription period {PeriodId} for group {GroupId}. All associated data preserved.",
            activePeriod.Id, groupId);
    }

    /// <inheritdoc />
    public async Task<SubscriptionPeriod?> GetCurrentPeriodAsync(Guid spaceId, Guid groupId, CancellationToken ct)
    {
        return await _db.SubscriptionPeriods.AsNoTracking()
            .FirstOrDefaultAsync(sp =>
                sp.SpaceId == spaceId && sp.GroupId == groupId && sp.Status == "active", ct);
    }
}
