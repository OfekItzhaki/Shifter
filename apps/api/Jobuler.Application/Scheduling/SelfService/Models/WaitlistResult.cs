namespace Jobuler.Application.Scheduling.SelfService.Models;

/// <summary>
/// Result of a waitlist join attempt.
/// </summary>
/// <param name="Success">Whether the member was successfully added to the waitlist.</param>
/// <param name="Position">The member's position in the waitlist, if joined successfully.</param>
/// <param name="ErrorMessage">Reason for failure, if the join was rejected.</param>
public record WaitlistResult(
    bool Success,
    int? Position,
    string? ErrorMessage);
