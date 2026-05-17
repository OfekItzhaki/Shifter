// Feature: cross-group-conflict-detection
// Property-based tests for ConflictDetector domain logic and deduplication hash.
// Tests use randomized inputs (100+ iterations per property) via [MemberData] generators.

using FluentAssertions;
using Jobuler.Domain.Conflicts;
using Jobuler.Infrastructure.Conflicts;
using Xunit;

namespace Jobuler.Tests.Conflicts;

public class ConflictDetectorPropertyTests
{
    private const int Iterations = 150;
    private static readonly DateTime BaseTime = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static FlatAssignment MakeAssignment(
        Guid? groupId = null,
        DateTime? startsAt = null,
        DateTime? endsAt = null,
        Guid? assignmentId = null,
        string? groupName = null)
    {
        return new FlatAssignment(
            AssignmentId: assignmentId ?? Guid.NewGuid(),
            GroupId: groupId ?? Guid.NewGuid(),
            GroupName: groupName ?? "Group",
            TaskSlotId: Guid.NewGuid(),
            StartsAt: startsAt ?? BaseTime,
            EndsAt: endsAt ?? BaseTime.AddHours(8));
    }

    /// <summary>
    /// Generates random assignment pairs from two different groups with random time ranges.
    /// Used for overlap and mutual exclusivity tests.
    /// </summary>
    public static IEnumerable<object[]> RandomAssignmentPairsFromDifferentGroups()
    {
        var rng = new Random(42); // Fixed seed for reproducibility
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();

        for (var i = 0; i < Iterations; i++)
        {
            var startA = BaseTime.AddHours(rng.Next(0, 200));
            var durationA = TimeSpan.FromMinutes(rng.Next(30, 720)); // 30 min to 12 hours
            var endA = startA + durationA;

            var startB = BaseTime.AddHours(rng.Next(0, 200));
            var durationB = TimeSpan.FromMinutes(rng.Next(30, 720));
            var endB = startB + durationB;

            yield return new object[] { groupA, groupB, startA, endA, startB, endB };
        }
    }

    /// <summary>
    /// Generates non-overlapping assignment pairs from different groups with varying rest settings.
    /// </summary>
    public static IEnumerable<object[]> NonOverlappingPairsWithRestSettings()
    {
        var rng = new Random(123);
        var groupA = Guid.NewGuid();
        var groupB = Guid.NewGuid();

        for (var i = 0; i < Iterations; i++)
        {
            var startA = BaseTime.AddHours(rng.Next(0, 100));
            var durationA = TimeSpan.FromHours(rng.Next(1, 12));
            var endA = startA + durationA;

            // Ensure B starts after A ends (non-overlapping)
            var gapMinutes = rng.Next(0, 1440); // 0 to 24 hours gap in minutes
            var startB = endA.AddMinutes(gapMinutes);
            var durationB = TimeSpan.FromHours(rng.Next(1, 12));
            var endB = startB + durationB;

            var restA = rng.Next(0, 16); // 0 to 15 hours
            var restB = rng.Next(0, 16);

            yield return new object[] { groupA, groupB, startA, endA, startB, endB, restA, restB };
        }
    }

    /// <summary>
    /// Generates sets of assignments all sharing the same GroupId.
    /// </summary>
    public static IEnumerable<object[]> SameGroupAssignmentSets()
    {
        var rng = new Random(777);

        for (var i = 0; i < Iterations; i++)
        {
            var groupId = Guid.NewGuid();
            var count = rng.Next(2, 8); // 2 to 7 assignments
            var assignments = new List<FlatAssignment>();

            for (var j = 0; j < count; j++)
            {
                var start = BaseTime.AddHours(rng.Next(0, 48));
                var duration = TimeSpan.FromHours(rng.Next(1, 12));
                assignments.Add(new FlatAssignment(
                    AssignmentId: Guid.NewGuid(),
                    GroupId: groupId,
                    GroupName: "SameGroup",
                    TaskSlotId: Guid.NewGuid(),
                    StartsAt: start,
                    EndsAt: start + duration));
            }

            yield return new object[] { assignments.ToArray() };
        }
    }

    /// <summary>
    /// Generates random ConflictPair lists for deduplication hash order-independence testing.
    /// </summary>
    public static IEnumerable<object[]> RandomConflictPairLists()
    {
        var rng = new Random(999);

        for (var i = 0; i < Iterations; i++)
        {
            var count = rng.Next(1, 8); // 1 to 7 conflict pairs
            var pairs = new List<ConflictPair>();

            for (var j = 0; j < count; j++)
            {
                var a = new FlatAssignment(
                    Guid.NewGuid(), Guid.NewGuid(), "GroupA", Guid.NewGuid(),
                    BaseTime.AddHours(rng.Next(0, 100)),
                    BaseTime.AddHours(rng.Next(100, 200)));
                var b = new FlatAssignment(
                    Guid.NewGuid(), Guid.NewGuid(), "GroupB", Guid.NewGuid(),
                    BaseTime.AddHours(rng.Next(0, 100)),
                    BaseTime.AddHours(rng.Next(100, 200)));
                var type = rng.Next(2) == 0 ? ConflictType.Overlap : ConflictType.RestViolation;
                pairs.Add(new ConflictPair(a, b, type));
            }

            yield return new object[] { pairs.ToArray() };
        }
    }

    // ── Property 1: Overlap detection is symmetric and complete ───────────────
    // Feature: cross-group-conflict-detection, Property 1: Overlap detection is symmetric and complete
    // Validates: Requirements 1.2, 3.1

    [Theory]
    [MemberData(nameof(RandomAssignmentPairsFromDifferentGroups))]
    public void Property1_OverlapDetection_IsSymmetricAndComplete(
        Guid groupA, Guid groupB, DateTime startA, DateTime endA, DateTime startB, DateTime endB)
    {
        // Arrange
        var assignmentA = MakeAssignment(groupId: groupA, startsAt: startA, endsAt: endA);
        var assignmentB = MakeAssignment(groupId: groupB, startsAt: startB, endsAt: endB);

        // The mathematical overlap condition
        var expectedOverlap = startA < endB && startB < endA;

        // Detect with order [A, B]
        var resultAB = ConflictDetector.Detect(
            new[] { assignmentA, assignmentB },
            (_, _) => 0); // No rest requirement — isolate overlap logic

        // Detect with order [B, A]
        var resultBA = ConflictDetector.Detect(
            new[] { assignmentB, assignmentA },
            (_, _) => 0);

        // Assert: classification matches the interval intersection formula
        var hasOverlapAB = resultAB.Conflicts.Any(c => c.Type == ConflictType.Overlap);
        var hasOverlapBA = resultBA.Conflicts.Any(c => c.Type == ConflictType.Overlap);

        hasOverlapAB.Should().Be(expectedOverlap,
            $"A=[{startA:HH:mm}–{endA:HH:mm}], B=[{startB:HH:mm}–{endB:HH:mm}] " +
            $"should {(expectedOverlap ? "" : "NOT ")}be classified as overlap");

        // Assert: symmetric — same result regardless of input order
        hasOverlapBA.Should().Be(hasOverlapAB,
            "overlap detection must be symmetric: order of inputs should not matter");
    }

    // ── Property 2: Rest violation uses the stricter threshold ────────────────
    // Feature: cross-group-conflict-detection, Property 2: Rest violation uses the stricter threshold
    // Validates: Requirements 4.1, 4.2, 4.4

    [Theory]
    [MemberData(nameof(NonOverlappingPairsWithRestSettings))]
    public void Property2_RestViolation_UsesStricterThreshold(
        Guid groupA, Guid groupB, DateTime startA, DateTime endA, DateTime startB, DateTime endB,
        int restA, int restB)
    {
        // Arrange
        var assignmentA = MakeAssignment(groupId: groupA, startsAt: startA, endsAt: endA);
        var assignmentB = MakeAssignment(groupId: groupB, startsAt: startB, endsAt: endB);

        int GetMinRest(Guid gA, Guid gB)
        {
            var rA = gA == groupA ? restA : restB;
            var rB = gB == groupA ? restA : restB;
            return Math.Max(rA, rB);
        }

        var maxRest = Math.Max(restA, restB);
        var gap = startB - endA; // B starts after A ends (non-overlapping by construction)
        var expectedViolation = maxRest > 0 && gap < TimeSpan.FromHours(maxRest);

        // Act
        var result = ConflictDetector.Detect(
            new[] { assignmentA, assignmentB },
            GetMinRest);

        // Assert
        var hasRestViolation = result.Conflicts.Any(c => c.Type == ConflictType.RestViolation);

        hasRestViolation.Should().Be(expectedViolation,
            $"gap={gap.TotalHours:F1}h, maxRest={maxRest}h → " +
            $"should {(expectedViolation ? "" : "NOT ")}be a rest violation");

        // Additional: when both groups have MinRest = 0, no rest violation
        if (restA == 0 && restB == 0)
        {
            hasRestViolation.Should().BeFalse(
                "when both groups have MinRestBetweenShiftsHours = 0, no rest violation should be flagged");
        }
    }

    // ── Property 3: Overlap and rest violation are mutually exclusive ─────────
    // Feature: cross-group-conflict-detection, Property 3: Overlap and rest violation are mutually exclusive
    // Validates: Requirements 4.3

    [Theory]
    [MemberData(nameof(RandomAssignmentPairsFromDifferentGroups))]
    public void Property3_OverlapAndRestViolation_AreMutuallyExclusive(
        Guid groupA, Guid groupB, DateTime startA, DateTime endA, DateTime startB, DateTime endB)
    {
        // Arrange
        var assignmentA = MakeAssignment(groupId: groupA, startsAt: startA, endsAt: endA);
        var assignmentB = MakeAssignment(groupId: groupB, startsAt: startB, endsAt: endB);

        // Use a non-zero rest requirement to allow rest violations to be detected
        var result = ConflictDetector.Detect(
            new[] { assignmentA, assignmentB },
            (_, _) => 12); // 12 hours rest requirement

        // Assert: no pair is classified as BOTH overlap and rest violation
        var overlapPairs = result.Conflicts
            .Where(c => c.Type == ConflictType.Overlap)
            .Select(c => (c.A.AssignmentId, c.B.AssignmentId))
            .ToHashSet();

        var restViolationPairs = result.Conflicts
            .Where(c => c.Type == ConflictType.RestViolation)
            .Select(c => (c.A.AssignmentId, c.B.AssignmentId))
            .ToHashSet();

        overlapPairs.Intersect(restViolationPairs).Should().BeEmpty(
            "no assignment pair should be classified as both Overlap and RestViolation");
    }

    // ── Property 4: Same-group assignments never produce conflicts ───────────
    // Feature: cross-group-conflict-detection, Property 4: Same-group assignments never produce conflicts
    // Validates: Requirements 3.2

    [Theory]
    [MemberData(nameof(SameGroupAssignmentSets))]
    public void Property4_SameGroupAssignments_NeverProduceConflicts(FlatAssignment[] assignments)
    {
        // Act — detect with a generous rest requirement to maximize chance of false positives
        var result = ConflictDetector.Detect(
            assignments,
            (_, _) => 24); // 24 hours rest — very strict, but same-group should still produce zero

        // Assert
        result.Conflicts.Should().BeEmpty(
            "assignments all sharing the same GroupId must never produce conflicts, " +
            $"regardless of time overlaps (tested with {assignments.Length} assignments)");
    }

    // ── Property 5: Deduplication fingerprint is order-independent ────────────
    // Feature: cross-group-conflict-detection, Property 5: Deduplication fingerprint is order-independent
    // Validates: Requirements 8.1

    [Theory]
    [MemberData(nameof(RandomConflictPairLists))]
    public void Property5_DeduplicationHash_IsOrderIndependent(ConflictPair[] pairs)
    {
        // Arrange — compute hash with original order
        var originalHash = ConflictDetectionService.ComputeDeduplicationHash(pairs);

        // Shuffle the pairs using a different permutation
        var rng = new Random(pairs.Length + pairs[0].A.AssignmentId.GetHashCode());
        var shuffled = pairs.OrderBy(_ => rng.Next()).ToArray();

        // Act — compute hash with shuffled order
        var shuffledHash = ConflictDetectionService.ComputeDeduplicationHash(shuffled);

        // Assert
        shuffledHash.Should().Be(originalHash,
            "deduplication hash must be identical regardless of the order conflict pairs are presented");

        // Additional: verify hash is a valid 64-char hex string (SHA-256)
        originalHash.Should().HaveLength(64, "SHA-256 hex string should be 64 characters");
        originalHash.Should().MatchRegex("^[0-9a-f]{64}$", "hash should be lowercase hex");
    }

    // ── Additional edge-case coverage for Property 5 ─────────────────────────

    [Fact]
    public void Property5_DeduplicationHash_SwappedPairMembers_SameHash()
    {
        // Verify that swapping A and B within a pair produces the same hash
        // (the algorithm uses min/max ordering of assignment IDs)
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();

        var assignmentA = new FlatAssignment(idA, Guid.NewGuid(), "G1", Guid.NewGuid(), BaseTime, BaseTime.AddHours(8));
        var assignmentB = new FlatAssignment(idB, Guid.NewGuid(), "G2", Guid.NewGuid(), BaseTime, BaseTime.AddHours(8));

        var pairAB = new ConflictPair(assignmentA, assignmentB, ConflictType.Overlap);
        var pairBA = new ConflictPair(assignmentB, assignmentA, ConflictType.Overlap);

        var hashAB = ConflictDetectionService.ComputeDeduplicationHash(new[] { pairAB });
        var hashBA = ConflictDetectionService.ComputeDeduplicationHash(new[] { pairBA });

        hashAB.Should().Be(hashBA,
            "swapping A and B within a conflict pair should produce the same deduplication hash");
    }
}
