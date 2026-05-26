namespace Jobuler.Application.Scheduling.SelfService.Models;

/// <summary>
/// Result of processing a shift request submission.
/// </summary>
/// <param name="Success">Whether the request was approved.</param>
/// <param name="ShiftRequestId">The ID of the created shift request, if approved.</param>
/// <param name="RejectionReason">Reason for rejection, if rejected.</param>
/// <param name="AlternativeSlots">Up to 5 alternative available slots for the same day when rejected due to full capacity.</param>
public record ShiftRequestResult(
    bool Success,
    Guid? ShiftRequestId,
    string? RejectionReason,
    IReadOnlyList<AvailableSlotDto>? AlternativeSlots);
