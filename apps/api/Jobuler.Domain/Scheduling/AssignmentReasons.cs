namespace Jobuler.Domain.Scheduling;

/// <summary>
/// Well-known assignment change reason strings used in ChangeReasonSummary.
/// Centralizes magic strings to prevent fragile string-based categorization.
/// </summary>
public static class AssignmentReasons
{
    /// <summary>
    /// Prefix used when an admin manually overrides an assignment.
    /// The full reason may include additional context (e.g., "Manual override by user {id}").
    /// </summary>
    public const string ManualOverride = "Manual override";
}
