using Jobuler.Application.Scheduling.SelfService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Recurring background service that processes expired waitlist offers every 5 minutes.
/// Marks timed-out offers as expired and cascades to the next waiting member.
/// </summary>
public class ProcessExpiredWaitlistOffersJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessExpiredWaitlistOffersJob> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    public ProcessExpiredWaitlistOffersJob(IServiceScopeFactory scopeFactory, ILogger<ProcessExpiredWaitlistOffersJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait 1 minute after startup before first run to let the app stabilize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredOffersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ProcessExpiredWaitlistOffersJob failed during execution");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessExpiredOffersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var waitlistService = scope.ServiceProvider.GetRequiredService<IWaitlistService>();

        _logger.LogDebug("ProcessExpiredWaitlistOffersJob: checking for expired waitlist offers");

        await waitlistService.ProcessExpiredOffersAsync(ct);

        _logger.LogDebug("ProcessExpiredWaitlistOffersJob: completed processing expired offers");
    }
}
