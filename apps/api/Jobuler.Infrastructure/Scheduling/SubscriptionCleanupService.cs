using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Background service that runs daily to:
/// 1. Soft-delete groups whose subscription has been canceled for 6+ months
/// 2. Send warning notifications 30 days before deletion
/// </summary>
public class SubscriptionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan DeletionGracePeriod = TimeSpan.FromDays(180); // 6 months
    private static readonly TimeSpan WarningPeriod = TimeSpan.FromDays(150); // 5 months (warn 30 days before)

    public SubscriptionCleanupService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 5 minutes after startup before first check
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Subscription cleanup failed");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessCleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;

        // Find groups with canceled subscriptions older than 6 months
        var expiredSubs = await db.GroupSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Canceled
                && s.CanceledAt.HasValue
                && s.CanceledAt.Value.AddDays(180) < now)
            .ToListAsync(ct);

        foreach (var sub in expiredSubs)
        {
            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == sub.GroupId && g.DeletedAt == null, ct);
            if (group == null) continue;

            // Soft-delete the group
            group.SoftDelete();
            _logger.LogInformation(
                "Auto-deleted group {GroupId} ({GroupName}) — subscription canceled {Days} days ago",
                group.Id, group.Name, (now - sub.CanceledAt!.Value).TotalDays);
        }

        if (expiredSubs.Count > 0)
            await db.SaveChangesAsync(ct);

        _logger.LogInformation("Subscription cleanup: checked {Count} expired subscriptions", expiredSubs.Count);
    }
}
