namespace Jobuler.Domain.Conflicts;

/// <summary>
/// The result of running conflict detection for a single person's assignments.
/// Contains all detected conflict pairs (overlaps and rest violations).
/// </summary>
public record ConflictResult(IReadOnlyList<ConflictPair> Conflicts);
