using Jobuler.Application.Billing;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Application.Billing.Commands;

/// <summary>
/// One-time migration command that converts group-level billing to space-level billing.
/// Admin-only — no user permission check (restricted to platform admins at the API layer).
/// Processes spaces in batches with per-batch transactions.
/// </summary>
public record MigrateToSpaceBillingCommand(int BatchSize = 100) : IRequest<MigrationResult>;

public record MigrationResult(
    int TotalSpacesProcessed,
    int SpacesSkipped,
    int SpacesMigratedActive,
    int SpacesMigratedTrialing,
    int BatchesFailed,
    List<string> Errors);

public class MigrateToSpaceBillingCommandHandler : IRequestHandler<MigrateToSpaceBillingCommand, MigrationResult>
{
    private readonly AppDbContext _db;
    private readonly ITrialDurationCache _trialDurationCache;
    private readonly ILogger<MigrateToSpaceBillingCommandHandler> _logger;

    public MigrateToSpaceBillingCommandHandler(
        AppDbContext db,
        ITrialDurationCache trialDurationCache,
        ILogger<MigrateToSpaceBillingCommandHandler> logger)
    {
        _db = db;
        _trialDurationCache = trialDurationCache;
        _logger = logger;
    }

    public async Task<MigrationResult> Handle(MigrateToSpaceBillingCommand request, CancellationToken ct)
    {
        var batchSize = request.BatchSize > 0 ? request.BatchSize : 100;

        _logger.LogInformation(
            "Starting migration from group-level to space-level billing with batch size {BatchSize}.",
            batchSize);

        // ── Determine which spaces to skip (already have a SpaceSubscription) ─── Req 8.4
        var existingSpaceSubSpaceIds = (await _db.SpaceSubscriptions
            .Select(ss => ss.SpaceId)
            .ToListAsync(ct))
            .ToHashSet();

        // ── Get distinct space IDs from non-migrated GroupSubscriptions ──────────
        var allCandidateSpaceIds = await _db.GroupSubscriptions
            .Where(gs => gs.Status != SubscriptionStatus.Migrated)
            .Select(gs => gs.SpaceId)
            .Distinct()
            .ToListAsync(ct);

        var spaceIdsToMigrate = allCandidateSpaceIds
            .Where(spaceId => !existingSpaceSubSpaceIds.Contains(spaceId))
            .ToList();

        var skipped = existingSpaceSubSpaceIds.Count;

        _logger.LogInformation(
            "Found {Count} spaces to migrate ({Skipped} already have SpaceSubscriptions).",
            spaceIdsToMigrate.Count, skipped);

        // ── Get trial days for spaces without active subscriptions ────────────────
        var trialDays = await _trialDurationCache.GetTrialDaysAsync(ct);

        // ── Process in batches ────────────────────────────────────────────────────
        var totalProcessed = 0;
        var migratedActive = 0;
        var migratedTrialing = 0;
        var batchesFailed = 0;
        var errors = new List<string>();

        var batches = spaceIdsToMigrate.Chunk(batchSize).ToList();

        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            var batch = batches[batchIndex];

            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(ct);

                foreach (var spaceId in batch)
                {
                    // Load all GroupSubscriptions for this space
                    var groupSubs = await _db.GroupSubscriptions
                        .Where(gs => gs.SpaceId == spaceId)
                        .ToListAsync(ct);

                    // Determine migration type BEFORE marking as migrated
                    var activeOrTrialingSubs = groupSubs
                        .Where(gs => gs.Status == SubscriptionStatus.Active
                                  || gs.Status == SubscriptionStatus.Trialing)
                        .ToList();

                    // Mark all GroupSubscriptions as Migrated (Req 8.1)
                    foreach (var gs in groupSubs)
                    {
                        gs.UpdateStatus(SubscriptionStatus.Migrated);
                    }

                    if (activeOrTrialingSubs.Count > 0)
                    {
                        // Find the one with the latest CurrentPeriodEnd (Req 8.2)
                        var latestSub = activeOrTrialingSubs
                            .Where(gs => gs.CurrentPeriodEnd.HasValue)
                            .OrderByDescending(gs => gs.CurrentPeriodEnd)
                            .FirstOrDefault() ?? activeOrTrialingSubs.First();

                        var spaceSub = SpaceSubscription.CreateTrial(spaceId, trialDays);
                        spaceSub.Activate(
                            tierId: latestSub.TierId,
                            lsSubscriptionId: latestSub.LemonSqueezySubscriptionId ?? "",
                            lsCustomerId: latestSub.LemonSqueezyCustomerId ?? "",
                            periodStart: latestSub.CurrentPeriodStart ?? DateTime.UtcNow,
                            periodEnd: latestSub.CurrentPeriodEnd ?? DateTime.UtcNow.AddDays(30));

                        _db.SpaceSubscriptions.Add(spaceSub);
                        migratedActive++;
                    }
                    else
                    {
                        // No active/trialing — create as Trialing (Req 8.3)
                        var spaceSub = SpaceSubscription.CreateTrial(spaceId, trialDays);
                        _db.SpaceSubscriptions.Add(spaceSub);
                        migratedTrialing++;
                    }

                    totalProcessed++;
                }

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                _logger.LogInformation(
                    "Batch {BatchIndex}/{TotalBatches} completed. Processed {Count} spaces.",
                    batchIndex + 1, batches.Count, batch.Length);
            }
            catch (Exception ex)
            {
                // Transaction is automatically rolled back on dispose (Req 8.5)
                batchesFailed++;
                var errorMsg = $"Batch {batchIndex + 1} failed: {ex.Message}";
                errors.Add(errorMsg);

                _logger.LogError(ex,
                    "Batch {BatchIndex}/{TotalBatches} failed. Rolling back {Count} spaces in this batch.",
                    batchIndex + 1, batches.Count, batch.Length);

                // Clear the change tracker to avoid stale state from the failed batch
                _db.ChangeTracker.Clear();
            }
        }

        _logger.LogInformation(
            "Migration complete. Processed: {Processed}, Active: {Active}, Trialing: {Trialing}, Failed batches: {Failed}.",
            totalProcessed, migratedActive, migratedTrialing, batchesFailed);

        return new MigrationResult(
            TotalSpacesProcessed: totalProcessed,
            SpacesSkipped: skipped,
            SpacesMigratedActive: migratedActive,
            SpacesMigratedTrialing: migratedTrialing,
            BatchesFailed: batchesFailed,
            Errors: errors);
    }
}
