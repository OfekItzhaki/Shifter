namespace Jobuler.Application.Scheduling.SelfService.Models;

/// <summary>
/// Result of querying available shift slots for a member.
/// Wraps the slot list with metadata about the request window state.
/// </summary>
/// <param name="Slots">Available shift slots for the member.</param>
/// <param name="IsReadOnly">True when the request window is closed and requests are not accepted.</param>
/// <param name="Message">Informational message when the window is closed, null otherwise.</param>
public record SlotAvailabilityResult(
    IReadOnlyList<AvailableSlotDto> Slots,
    bool IsReadOnly,
    string? Message);
