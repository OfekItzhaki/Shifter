namespace Jobuler.Domain.Conflicts;

/// <summary>
/// A flattened view of an assignment with its time range and group context,
/// used as input to the conflict detection algorithm.
/// </summary>
public record FlatAssignment(
    Guid AssignmentId,
    Guid GroupId,
    string GroupName,
    Guid TaskSlotId,
    DateTime StartsAt,
    DateTime EndsAt);
