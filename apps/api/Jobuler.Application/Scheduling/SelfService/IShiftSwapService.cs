using Jobuler.Application.Scheduling.SelfService.Models;

namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Manages shift swap proposals between members.
/// Validates ownership, conflict detection, and atomic reassignment of shifts.
/// </summary>
public interface IShiftSwapService
{
    /// <summary>
    /// Proposes a swap between the initiator's shift and the target member's shift.
    /// Validates both requests are approved, belong to the same group, and both shifts are in the future.
    /// Creates a swap request with a 72-hour expiry.
    /// </summary>
    /// <param name="initiatorPersonId">The member proposing the swap.</param>
    /// <param name="initiatorRequestId">The initiator's approved shift request to offer.</param>
    /// <param name="targetRequestId">The target member's approved shift request to request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success with swap request ID or failure with reason.</returns>
    Task<SwapResult> ProposeSwapAsync(Guid initiatorPersonId, Guid initiatorRequestId, Guid targetRequestId, CancellationToken ct = default);

    /// <summary>
    /// Accepts a pending swap request. Validates no time-overlap or rest-period conflicts
    /// using the ConflictDetector, then atomically reassigns both shifts.
    /// </summary>
    /// <param name="targetPersonId">The target member accepting the swap.</param>
    /// <param name="swapRequestId">The swap request to accept.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure with conflict details.</returns>
    Task<SwapResult> AcceptSwapAsync(Guid targetPersonId, Guid swapRequestId, CancellationToken ct = default);

    /// <summary>
    /// Declines a pending swap request. Marks it as declined and notifies the initiator.
    /// </summary>
    /// <param name="targetPersonId">The target member declining the swap.</param>
    /// <param name="swapRequestId">The swap request to decline.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeclineSwapAsync(Guid targetPersonId, Guid swapRequestId, CancellationToken ct = default);

    /// <summary>
    /// Cancels a pending swap request initiated by the caller.
    /// Only the initiator can cancel, and only while the request is still pending.
    /// </summary>
    /// <param name="initiatorPersonId">The initiator cancelling their swap proposal.</param>
    /// <param name="swapRequestId">The swap request to cancel.</param>
    /// <param name="ct">Cancellation token.</param>
    Task CancelSwapAsync(Guid initiatorPersonId, Guid swapRequestId, CancellationToken ct = default);
}
