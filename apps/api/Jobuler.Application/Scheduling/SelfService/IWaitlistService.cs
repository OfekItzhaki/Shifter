using Jobuler.Application.Scheduling.SelfService.Models;

namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Manages the waitlist for full shift slots.
/// Handles joining, leaving, slot release cascading, and expired offer processing.
/// </summary>
public interface IWaitlistService
{
    /// <summary>
    /// Adds a member to the waitlist for a full shift slot.
    /// Assigns the next available position. Rejects duplicates.
    /// </summary>
    /// <param name="personId">The member joining the waitlist.</param>
    /// <param name="shiftSlotId">The full shift slot to wait for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success with position or failure with reason.</returns>
    Task<WaitlistResult> JoinWaitlistAsync(Guid personId, Guid shiftSlotId, CancellationToken ct = default);

    /// <summary>
    /// Removes a member from the waitlist for a shift slot.
    /// If the member has an active offer, treats removal as a decline and cascades to the next member.
    /// </summary>
    /// <param name="personId">The member leaving the waitlist.</param>
    /// <param name="shiftSlotId">The shift slot waitlist to leave.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LeaveWaitlistAsync(Guid personId, Guid shiftSlotId, CancellationToken ct = default);

    /// <summary>
    /// Processes a slot release (due to cancellation or admin removal).
    /// Offers the slot to the first waiting member with a configurable acceptance period.
    /// </summary>
    /// <param name="shiftSlotId">The shift slot that now has available capacity.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ProcessSlotReleasedAsync(Guid shiftSlotId, CancellationToken ct = default);

    /// <summary>
    /// Processes all expired waitlist offers across all slots.
    /// Marks expired offers and cascades to the next waiting member.
    /// Called periodically by a background job.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task ProcessExpiredOffersAsync(CancellationToken ct = default);
}
