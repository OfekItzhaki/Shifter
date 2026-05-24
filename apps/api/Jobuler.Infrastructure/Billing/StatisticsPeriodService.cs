using Jobuler.Application.Billing;
using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Billing;

/// <summary>
/// Manages statistics period boundaries in response to subscription lifecycle events.
/// Each lifecycle event closes active periods and opens new ones for all groups in the space.
/// </summary>
public class StatisticsPeriodService : IStatisticsPeriodService
{
    private readonly AppDbContext _db;
    private readonly ILogger<StatisticsPeriodService> _logger;

    public StatisticsPeriodService(AppDbContext db, ILogger<StatisticsPeriodService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnTrialStartedAsync(Guid spaceId, DateTime startBoundary, CancellationToken ct)
    {
        var groupIds = await GetActiveGroupIdsAsync(spaceId, ct);
        if (groupIds.Count == 0)
        {
            _logger.LogWarning(
                "No groups found in space {SpaceId} at trial start. Skipping period creation for reconciliation.",
                spaceId);
            return;
        }

        await OpenPeriodsAsync(spaceId, groupIds, startBoundary, ct);

        _logger.LogInformation(
            "Trial started for space {SpaceId}: opened {Count} statistics periods with boundary {Boundary}.",
            spaceId, groupIds.Count, startBoundary);
    }

    /// <inheritdoc />
    public async Task OnTrialExpiredAsync(Guid spaceId, DateTime endBoundary, CancellationToken ct)
    {
        var groupIds = await GetActiveGroupIdsAsync(spaceId, ct);
        if (groupIds.Count == 0)
        {
            _logger.LogWarning(
                "No groups found in space {SpaceId} at trial expiry. Skipping period closure for reconciliation.",
                spaceId);
            return;
        }

        await CloseActivePeriodsAsync(spaceId, groupIds, endBoundary, ct);

        _logger.LogInformation(
            "Trial expired for space {SpaceId}: closed {Count} statistics periods with boundary {Boundary}.",
            spaceId, groupIds.Count, endBoundary);
    }

    /// <inheritdoc />
    public async Task OnSubscriptionActivatedAsync(Guid spaceId, DateTime startBoundary, CancellationToken ct)
    {
        var groupIds = await GetActiveGroupIdsAsync(spaceId, ct);
        if (groupIds.Count == 0)
        {
            _logger.LogWarning(
                "No groups found in space {SpaceId} at subscription activation. Skipping period rotation for reconciliation.",
                spaceId);
            return;
        }

        await CloseActivePeriodsAsync(spaceId, groupIds, startBoundary, ct);
        await OpenPeriodsAsync(spaceId, groupIds, startBoundary, ct);

        _logger.LogInformation(
            "Subscription activated for space {SpaceId}: rotated {Count} statistics periods with boundary {Boundary}.",
            spaceId, groupIds.Count, startBoundary);
    }

    /// <inheritdoc />
    public async Task OnSubscriptionExpiredAsync(Guid spaceId, DateTime endBoundary, CancellationToken ct)
    {
        var groupIds = await GetActiveGroupIdsAsync(spaceId, ct);
        if (groupIds.Count == 0)
        {
            _logger.LogWarning(
                "No groups found in space {SpaceId} at subscription expiry. Skipping period closure for reconciliation.",
                spaceId);
            return;
        }

        await CloseActivePeriodsAsync(spaceId, groupIds, endBoundary, ct);

        _logger.LogInformation(
            "Subscription expired for space {SpaceId}: closed {Count} statistics periods with boundary {Boundary}.",
            spaceId, groupIds.Count, endBoundary);
    }

    /// <inheritdoc />
    public async Task OnPeriodRenewedAsync(Guid spaceId, DateTime newPeriodStart, CancellationToken ct)
    {
        var groupIds = await GetActiveGroupIdsAsync(spaceId, ct);
        if (groupIds.Count == 0)
        {
            _logger.LogWarning(
                "No groups found in space {SpaceId} at period renewal. Skipping period rotation for reconciliation.",
                spaceId);
            return;
        }

        await CloseActivePeriodsAsync(spaceId, groupIds, newPeriodStart, ct);
        await OpenPeriodsAsync(spaceId, groupIds, newPeriodStart, ct);

        _logger.LogInformation(
            "Period renewed for space {SpaceId}: rotated {Count} statistics periods with boundary {Boundary}.",
            spaceId, groupIds.Count, newPeriodStart);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<List<Guid>> GetActiveGroupIdsAsync(Guid spaceId, CancellationToken ct)
    {
        return await _db.Groups.AsNoTracking()
            .Where(g => g.SpaceId == spaceId && g.DeletedAt == null)
            .Select(g => g.Id)
            .ToListAsync(ct);
    }

    private async Task CloseActivePeriodsAsync(
        Guid spaceId, List<Guid> groupIds, DateTime endBoundary, CancellationToken ct)
    {
        var activePeriods = await _db.SubscriptionPeriods
            .Where(sp => sp.SpaceId == spaceId
                && groupIds.Contains(sp.GroupId)
                && sp.Status == "active")
            .ToListAsync(ct);

        foreach (var period in activePeriods)
        {
            period.Close();
            // Override EndsAt to use the lifecycle boundary date instead of UtcNow
            _db.Entry(period).Property(p => p.EndsAt).CurrentValue = endBoundary;
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task OpenPeriodsAsync(
        Guid spaceId, List<Guid> groupIds, DateTime startBoundary, CancellationToken ct)
    {
        foreach (var groupId in groupIds)
        {
            var period = SubscriptionPeriod.Create(spaceId, groupId);
            _db.SubscriptionPeriods.Add(period);
            // Override StartsAt to use the lifecycle boundary date instead of UtcNow
            _db.Entry(period).Property(p => p.StartsAt).CurrentValue = startBoundary;
        }

        await _db.SaveChangesAsync(ct);
    }
}
