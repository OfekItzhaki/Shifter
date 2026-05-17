namespace Jobuler.Domain.Conflicts;

/// <summary>
/// Pure static conflict detection logic using a sort-then-sweep algorithm.
/// Detects overlapping assignments and rest violations for a single person's
/// assignments across multiple groups.
/// </summary>
public static class ConflictDetector
{
    /// <summary>
    /// Given a list of assignments (with time ranges and group info) for a single person,
    /// returns all overlap conflicts and rest violations.
    /// Only cross-group pairs are compared — same-group pairs are ignored.
    /// </summary>
    /// <param name="assignments">All assignments for a single person across groups.</param>
    /// <param name="getMinRestHours">
    /// Returns the maximum of the two groups' MinRestBetweenShiftsHours values
    /// for a given pair of GroupIds.
    /// </param>
    public static ConflictResult Detect(
        IReadOnlyList<FlatAssignment> assignments,
        Func<Guid, Guid, int> getMinRestHours)
    {
        if (assignments.Count < 2)
            return new ConflictResult([]);

        // Step 1: Sort by StartsAt ascending
        var sorted = assignments.OrderBy(a => a.StartsAt).ThenBy(a => a.EndsAt).ToList();

        var conflicts = new List<ConflictPair>();

        // Step 2: Sweep with an active set.
        // For each new assignment, compare against all active assignments from different groups.
        var active = new List<FlatAssignment>();

        for (var i = 0; i < sorted.Count; i++)
        {
            var current = sorted[i];

            // Determine the max possible rest hours we might need to consider.
            // We use a generous pruning window to avoid missing rest violations.
            // Active assignments whose EndsAt + maxPossibleRest <= current.StartsAt can be removed.
            PruneActiveSet(active, current.StartsAt, getMinRestHours);

            // Compare current against all active assignments from different groups
            foreach (var other in active)
            {
                if (other.GroupId == current.GroupId)
                    continue; // Same group — skip

                // Check for overlap: A.StartsAt < B.EndsAt AND B.StartsAt < A.EndsAt
                if (other.StartsAt < current.EndsAt && current.StartsAt < other.EndsAt)
                {
                    conflicts.Add(new ConflictPair(other, current, ConflictType.Overlap));
                }
                else
                {
                    // Non-overlapping: check rest violation
                    // Since sorted by StartsAt and other is in active set,
                    // the gap is current.StartsAt - other.EndsAt
                    var gap = current.StartsAt - other.EndsAt;

                    var minRest = getMinRestHours(other.GroupId, current.GroupId);

                    if (minRest > 0 && gap < TimeSpan.FromHours(minRest))
                    {
                        conflicts.Add(new ConflictPair(other, current, ConflictType.RestViolation));
                    }
                }
            }

            active.Add(current);
        }

        return new ConflictResult(conflicts);
    }

    /// <summary>
    /// Removes assignments from the active set that can no longer produce conflicts
    /// with the current assignment (or any future assignment).
    /// An active assignment can be pruned when current.StartsAt >= active.EndsAt + maxRestWindow,
    /// meaning it's too far in the past to overlap or violate rest with any future assignment.
    /// </summary>
    private static void PruneActiveSet(
        List<FlatAssignment> active,
        DateTime currentStartsAt,
        Func<Guid, Guid, int> getMinRestHours)
    {
        active.RemoveAll(a =>
        {
            // Conservative pruning: we can only remove if the gap is large enough
            // that no rest violation is possible. Since we don't know the max rest hours
            // for all possible future group pairs, we use the assignment's own group
            // against all possible groups. A safe upper bound is needed.
            // 
            // We use a fixed upper bound for pruning. If the gap exceeds this,
            // no rest violation is possible regardless of group settings.
            // Using 168 hours (7 days) as a safe upper bound — any real MinRestBetweenShiftsHours
            // would be far less than this.
            const int maxPossibleRestHours = 168;
            return currentStartsAt >= a.EndsAt.AddHours(maxPossibleRestHours);
        });
    }
}
