namespace Jobuler.Application.Scheduling.SelfService.Models;

/// <summary>
/// Result of a shift request cancellation attempt.
/// </summary>
/// <param name="Success">Whether the cancellation was processed successfully.</param>
/// <param name="ErrorMessage">Reason for failure, if the cancellation was rejected.</param>
public record CancellationResult(
    bool Success,
    string? ErrorMessage);
