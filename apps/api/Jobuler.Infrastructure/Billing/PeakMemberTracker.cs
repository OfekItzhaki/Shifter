using Jobuler.Application.Billing;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Billing;

/// <summary>
/// Tracks peak member count for space-level billing.
/// After a member is added to a space, loads the SpaceSubscription and updates
/// the peak if the current count exceeds the stored peak.
/// Designed to be lightweight — failures are logged but do not propagate.
/// </summary>
public class PeakMemberTracker : IPeakMemberTracker
{
    private readonly AppDbContext _db;
    private readonly ILogger<PeakMemberTracker> _logger;

    public PeakMemberTracker(AppDbContext db, ILogger<PeakMemberTracker> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task TrackAsync(Guid spaceId, CancellationToken ct = default)
    {
        try
        {
            var subscription = await _db.SpaceSubscriptions
                .FirstOrDefaultAsync(s => s.SpaceId == spaceId, ct);

            if (subscription is null)
                return;

            var currentMemberCount = await _db.People
                .CountAsync(p => p.SpaceId == spaceId && p.IsActive, ct);

            subscription.UpdatePeakMemberCount(currentMemberCount);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to update peak member count for space {SpaceId}. Billing tracking may be stale.",
                spaceId);
        }
    }
}
