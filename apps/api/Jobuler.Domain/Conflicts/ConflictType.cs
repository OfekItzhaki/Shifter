namespace Jobuler.Domain.Conflicts;

/// <summary>
/// The type of scheduling conflict detected between two cross-group assignments.
/// </summary>
public enum ConflictType
{
    /// <summary>
    /// Two assignments have overlapping time ranges.
    /// </summary>
    Overlap,

    /// <summary>
    /// The gap between two non-overlapping assignments is less than the required minimum rest hours.
    /// </summary>
    RestViolation
}
