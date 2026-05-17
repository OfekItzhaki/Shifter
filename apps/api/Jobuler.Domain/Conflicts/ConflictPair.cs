namespace Jobuler.Domain.Conflicts;

/// <summary>
/// A pair of assignments from different groups that are in conflict,
/// along with the type of conflict detected.
/// </summary>
public record ConflictPair(
    FlatAssignment A,
    FlatAssignment B,
    ConflictType Type);
