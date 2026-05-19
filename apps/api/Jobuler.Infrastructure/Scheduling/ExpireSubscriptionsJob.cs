using Jobuler.Application.Billing.Commands;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Recurring background service that dispatches <see cref="ExpireSubscriptionsCommand"/>
/// every 6 hours to transition canceled subscriptions past their billing period into
/// Expired status and deactivate their associated groups.
/// </summary>
public class ExpireSubscriptionsJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpireSubscriptionsJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    public ExpireSubscriptionsJob(IServiceScopeFactory scopeFactory, ILogger<ExpireSubscriptionsJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 2 minutes after startup before first run to let the app stabilize
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunExpiryAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExpireSubscriptionsJob failed during execution");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunExpiryAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var now = DateTime.UtcNow;

        // Count subscriptions eligible for expiry (same criteria as the command handler)
        var eligibleCount = await db.GroupSubscriptions
            .CountAsync(s =>
                (s.Status == SubscriptionStatus.Canceled && s.CurrentPeriodEnd < now) ||
                (s.Status == SubscriptionStatus.Canceled && s.TrialEndsAt < now && s.CurrentPeriodEnd == null),
                ct);

        if (eligibleCount == 0)
        {
            _logger.LogInformation("ExpireSubscriptionsJob: no subscriptions to expire");
            return;
        }

        _logger.LogInformation("ExpireSubscriptionsJob: found {Count} subscription(s) eligible for expiry", eligibleCount);

        await mediator.Send(new ExpireSubscriptionsCommand(), ct);

        _logger.LogInformation("ExpireSubscriptionsJob: expired {Count} subscription(s)", eligibleCount);
    }
}
