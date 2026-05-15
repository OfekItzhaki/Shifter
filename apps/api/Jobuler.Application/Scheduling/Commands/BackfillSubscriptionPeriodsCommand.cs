using Jobuler.Domain.Scheduling;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Scheduling.Commands;

/// <summary>
/// One-time backfill command that generates initial subscription periods for all existing groups.
/// For each group, creates a SubscriptionPeriod with starts_at = the group's created_at date
/// (or subscription start date if available). Idempotent — skips groups that already have a period.
/// </summary>
public record BackfillSubscriptionPeriodsCommand : IRequest<BackfillSubscriptionPeriodsResult>;

public record BackfillSubscriptionPeriodsResult(int Created, int Skipped);

public class BackfillSubscriptionPeriodsCommandHandler
    : IRequestHandler<BackfillSubscriptionPeriodsCommand, BackfillSubscriptionPeriodsResult>
{
    private readonly AppDbContext _db;
    private readonly ILogger<BackfillSubscriptionPeriodsCommandHandler> _logger;

    public BackfillSubscriptionPeriodsCommandHandler(
        AppDbContext db,
        ILogger<BackfillSubscriptionPeriodsCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<BackfillSubscriptionPeriodsResult> Handle(
        BackfillSubscriptionPeriodsCommand request, CancellationToken ct)
    {
        _logger.LogInformation("Starting subscription periods backfill...");

        // Get all active groups
        var groups = await _db.Groups.AsNoTracking()
            .Where(g => g.IsActive && g.DeletedAt == null)
            .Select(g => new { g.Id, g.SpaceId, g.CreatedAt })
            .ToListAsync(ct);

        // Get groups that already have a subscription period (any status)
        var groupsWithPeriods = await _db.SubscriptionPeriods.AsNoTracking()
            .Select(sp => sp.GroupId)
            .Distinct()
            .ToListAsync(ct);

        var groupsWithPeriodsSet = groupsWithPeriods.ToHashSet();

        // Get subscription start dates for groups that have subscriptions
        var subscriptionDates = await _db.GroupSubscriptions.AsNoTracking()
            .GroupBy(gs => gs.GroupId)
            .Select(g => new { GroupId = g.Key, EarliestCreatedAt = g.Min(s => s.CreatedAt) })
            .ToDictionaryAsync(x => x.GroupId, x => x.EarliestCreatedAt, ct);

        int created = 0;
        int skipped = 0;

        foreach (var group in groups)
        {
            if (groupsWithPeriodsSet.Contains(group.Id))
            {
                skipped++;
                continue;
            }

            // Use subscription start date if available, otherwise use group's created_at
            var startsAt = subscriptionDates.TryGetValue(group.Id, out var subDate)
                ? subDate
                : group.CreatedAt;

            var period = SubscriptionPeriod.Create(group.SpaceId, group.Id);

            // Override StartsAt to use the historical date instead of UtcNow
            // We use EF's entry to set the property directly since the factory sets StartsAt = UtcNow
            _db.SubscriptionPeriods.Add(period);
            _db.Entry(period).Property(p => p.StartsAt).CurrentValue = startsAt;

            created++;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Subscription periods backfill complete. Created: {Created}, Skipped: {Skipped}",
            created, skipped);

        return new BackfillSubscriptionPeriodsResult(created, skipped);
    }
}
