using Jobuler.Application.Scheduling.SelfService.Models;

namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Processes shift request submissions and cancellations for self-service scheduling.
/// Enforces capacity limits, request window constraints, and Max_Shifts validation.
/// Acquires advisory locks before reading slot capacity to ensure concurrency safety.
/// </summary>
public interface IShiftRequestService
{
    /// <summary>
    /// Processes a shift request for the given member and slot.
    /// Acquires an exclusive lock on the slot, validates capacity and constraints,
    /// and either approves or rejects the request.
    /// </summary>
    /// <param name="personId">The member requesting the shift.</param>
    /// <param name="shiftSlotId">The target shift slot.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating approval or rejection with reason and alternatives.</returns>
    Task<ShiftRequestResult> ProcessRequestAsync(Guid personId, Guid shiftSlotId, CancellationToken ct = default);

    /// <summary>
    /// Cancels a previously approved shift request.
    /// Validates cancellation window, decrements slot fill count, and triggers waitlist processing.
    /// </summary>
    /// <param name="personId">The member cancelling their request.</param>
    /// <param name="shiftRequestId">The shift request to cancel.</param>
    /// <param name="reason">Cancellation reason (1-500 characters).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure with reason.</returns>
    Task<CancellationResult> CancelRequestAsync(Guid personId, Guid shiftRequestId, string reason, CancellationToken ct = default);
}
