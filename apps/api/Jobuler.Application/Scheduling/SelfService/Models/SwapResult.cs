namespace Jobuler.Application.Scheduling.SelfService.Models;

/// <summary>
/// Result of a shift swap operation (propose or accept).
/// </summary>
/// <param name="Success">Whether the swap operation succeeded.</param>
/// <param name="SwapRequestId">The ID of the swap request, if created or accepted.</param>
/// <param name="ErrorMessage">Reason for failure, if the operation was rejected.</param>
public record SwapResult(
    bool Success,
    Guid? SwapRequestId,
    string? ErrorMessage);
