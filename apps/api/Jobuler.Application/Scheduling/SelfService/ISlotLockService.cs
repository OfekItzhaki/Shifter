namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Provides exclusive locking on shift slots using PostgreSQL advisory locks.
/// Prevents concurrent requests from claiming the same slot simultaneously.
/// Must be called within an active database transaction.
/// </summary>
public interface ISlotLockService
{
    /// <summary>
    /// Acquires a PostgreSQL advisory lock scoped to the given shift slot.
    /// The lock is automatically released when the enclosing transaction commits or rolls back.
    /// </summary>
    /// <param name="shiftSlotId">The shift slot to lock.</param>
    /// <param name="timeout">Maximum time to wait for lock acquisition (default: 5 seconds).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the lock was acquired within the timeout; false otherwise.</returns>
    Task<bool> TryAcquireSlotLockAsync(Guid shiftSlotId, TimeSpan timeout, CancellationToken ct = default);
}
