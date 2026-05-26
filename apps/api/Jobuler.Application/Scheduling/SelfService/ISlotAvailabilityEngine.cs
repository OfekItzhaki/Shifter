using Jobuler.Application.Scheduling.SelfService.Models;

namespace Jobuler.Application.Scheduling.SelfService;

/// <summary>
/// Queries available shift slots for a member within a scheduling cycle.
/// Filters out full slots, slots already claimed by the member, and slots
/// that conflict with the member's existing approved shifts.
/// </summary>
public interface ISlotAvailabilityEngine
{
    /// <summary>
    /// Returns all available shift slots for the given member in the specified cycle.
    /// Excludes slots at full capacity, slots the member already has a pending/approved request for,
    /// and slots that overlap in time with the member's existing approved shifts (exclusive endpoints).
    /// Results are sorted by date ascending, then start time ascending.
    /// When the request window is closed, returns the list with a read-only flag.
    /// </summary>
    /// <param name="personId">The member querying availability.</param>
    /// <param name="groupId">The group to query slots for.</param>
    /// <param name="schedulingCycleId">The scheduling cycle to query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Availability result containing slots and request window state.</returns>
    Task<SlotAvailabilityResult> GetAvailableSlotsAsync(
        Guid personId, Guid groupId, Guid schedulingCycleId, CancellationToken ct = default);
}
