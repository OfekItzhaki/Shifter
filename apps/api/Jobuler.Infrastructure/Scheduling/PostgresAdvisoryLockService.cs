using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Scheduling;

/// <summary>
/// Implements exclusive slot locking using PostgreSQL transaction-scoped advisory locks.
/// The lock is automatically released when the enclosing transaction commits or rolls back.
/// </summary>
public class PostgresAdvisoryLockService : ISlotLockService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PostgresAdvisoryLockService> _logger;

    public PostgresAdvisoryLockService(AppDbContext db, ILogger<PostgresAdvisoryLockService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> TryAcquireSlotLockAsync(Guid shiftSlotId, TimeSpan timeout, CancellationToken ct = default)
    {
        // Create a linked cancellation token that fires after the specified timeout
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            // pg_advisory_xact_lock blocks until the lock is acquired or the statement is cancelled.
            // hashtext converts the slot ID string to a stable int4 hash for the advisory lock key.
            // The lock is scoped to the current transaction and released on commit/rollback.
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtext({0}::text))",
                new object[] { shiftSlotId.ToString() },
                linkedCts.Token);

            _logger.LogDebug("Advisory lock acquired for slot {SlotId}", shiftSlotId);
            return true;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Timeout expired while waiting for the lock — not a caller cancellation
            _logger.LogWarning(
                "Advisory lock acquisition timed out after {Timeout}s for slot {SlotId}",
                timeout.TotalSeconds, shiftSlotId);
            return false;
        }
        // If the caller's ct was cancelled, let the OperationCanceledException propagate
    }
}
