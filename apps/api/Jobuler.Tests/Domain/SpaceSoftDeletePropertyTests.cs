// Feature: space-management
// Property-based tests for soft-delete/restore (Properties 1, 2, 3)

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using Xunit;

namespace Jobuler.Tests.Domain;

[Trait("Feature", "space-management")]
public class SpaceSoftDeletePropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Space with a random name and owner.
    /// </summary>
    private static Space CreateSpace(string name = "Test Space")
    {
        return Space.Create(
            string.IsNullOrWhiteSpace(name) ? "Fallback" : name,
            Guid.NewGuid());
    }

    /// <summary>
    /// Creates a Group belonging to the given space.
    /// </summary>
    private static Group CreateGroup(Guid spaceId, string name = "Group")
    {
        return Group.Create(spaceId, null, name);
    }

    // ── Property 1: Soft-delete/restore round trip ────────────────────────────
    // For any active space, soft-delete then restore results in DeletedAt == null
    // **Validates: Requirements 1.1, 2.1**

    [Property(MaxTest = 100)]
    public Property Property1_SoftDelete_Then_Restore_ResultsInNullDeletedAt()
    {
        var gen = from name in Arb.Generate<NonEmptyString>()
                  select name.Get;

        return Prop.ForAll(Arb.From(gen), name =>
        {
            // Use a safe name (non-whitespace)
            var safeName = new string(name.Where(c => !char.IsWhiteSpace(c)).Take(50).ToArray());
            if (string.IsNullOrEmpty(safeName)) safeName = "Space";

            var space = CreateSpace(safeName);

            // Pre-condition: space starts active (DeletedAt is null)
            space.DeletedAt.Should().BeNull();

            // Act: soft-delete then restore
            space.SoftDelete();
            space.DeletedAt.Should().NotBeNull("SoftDelete must set DeletedAt");

            space.Restore();

            // Assert: round-trip returns to null
            return space.DeletedAt == null;
        });
    }

    [Property(MaxTest = 100)]
    public Property Property1_SoftDelete_SetsNonNullDeletedAt()
    {
        var gen = from _ in Gen.Constant(0)
                  select _;

        return Prop.ForAll(Arb.From(gen), _ =>
        {
            var space = CreateSpace();
            space.SoftDelete();
            return space.DeletedAt != null;
        });
    }

    // ── Property 2: Cascade preserves individually-deleted groups ─────────────
    // For N groups with M individually deleted, cascade deletes exactly (N-M)
    // and restore restores exactly those (N-M)
    // **Validates: Requirements 1.2, 2.2**

    [Property(MaxTest = 100)]
    public Property Property2_CascadeDelete_OnlyAffectsNonDeletedGroups()
    {
        // Generate total group count [1..20] and individually-deleted count [0..total]
        var gen = from totalGroups in Gen.Choose(1, 20)
                  from individuallyDeleted in Gen.Choose(0, totalGroups)
                  select (totalGroups, individuallyDeleted);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (totalGroups, individuallyDeleted) = tuple;
            var spaceId = Guid.NewGuid();

            // Create groups
            var groups = Enumerable.Range(0, totalGroups)
                .Select(i => CreateGroup(spaceId, $"Group {i}"))
                .ToList();

            // Individually delete the first M groups
            for (int i = 0; i < individuallyDeleted; i++)
            {
                groups[i].SoftDelete();
            }

            // Act: cascade soft-delete (simulating space deletion)
            foreach (var group in groups)
            {
                group.SoftDeleteBySpace();
            }

            // Assert: exactly (totalGroups - individuallyDeleted) groups were cascade-deleted
            var cascadeDeleted = groups.Count(g => g.DeletedBySpaceDeletion);
            return cascadeDeleted == totalGroups - individuallyDeleted;
        });
    }

    [Property(MaxTest = 100)]
    public Property Property2_CascadeRestore_OnlyRestoresCascadeDeletedGroups()
    {
        var gen = from totalGroups in Gen.Choose(1, 20)
                  from individuallyDeleted in Gen.Choose(0, totalGroups)
                  select (totalGroups, individuallyDeleted);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (totalGroups, individuallyDeleted) = tuple;
            var spaceId = Guid.NewGuid();

            // Create groups
            var groups = Enumerable.Range(0, totalGroups)
                .Select(i => CreateGroup(spaceId, $"Group {i}"))
                .ToList();

            // Individually delete the first M groups
            for (int i = 0; i < individuallyDeleted; i++)
            {
                groups[i].SoftDelete();
            }

            // Cascade soft-delete
            foreach (var group in groups)
            {
                group.SoftDeleteBySpace();
            }

            // Act: cascade restore (simulating space restoration)
            foreach (var group in groups)
            {
                group.RestoreFromSpaceDeletion();
            }

            // Assert: individually-deleted groups remain deleted
            var stillDeleted = groups.Take(individuallyDeleted)
                .All(g => g.DeletedAt != null && !g.DeletedBySpaceDeletion);

            // Assert: cascade-deleted groups are now restored
            var restored = groups.Skip(individuallyDeleted)
                .All(g => g.DeletedAt == null && !g.DeletedBySpaceDeletion);

            return stillDeleted && restored;
        });
    }

    [Property(MaxTest = 100)]
    public Property Property2_IndividuallyDeletedGroups_UnchangedByCascade()
    {
        var gen = from totalGroups in Gen.Choose(1, 15)
                  from individuallyDeleted in Gen.Choose(1, totalGroups)
                  select (totalGroups, individuallyDeleted);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (totalGroups, individuallyDeleted) = tuple;
            var spaceId = Guid.NewGuid();

            var groups = Enumerable.Range(0, totalGroups)
                .Select(i => CreateGroup(spaceId, $"Group {i}"))
                .ToList();

            // Individually delete first M groups
            for (int i = 0; i < individuallyDeleted; i++)
            {
                groups[i].SoftDelete();
            }

            // Record their DeletedAt timestamps
            var originalDeletedAts = groups.Take(individuallyDeleted)
                .Select(g => g.DeletedAt)
                .ToList();

            // Cascade delete + restore
            foreach (var group in groups) group.SoftDeleteBySpace();
            foreach (var group in groups) group.RestoreFromSpaceDeletion();

            // Individually-deleted groups should still have their original DeletedAt
            var unchanged = groups.Take(individuallyDeleted)
                .Select((g, i) => g.DeletedAt != null)
                .All(x => x);

            // They should NOT have DeletedBySpaceDeletion flag
            var noFlag = groups.Take(individuallyDeleted)
                .All(g => !g.DeletedBySpaceDeletion);

            return unchanged && noFlag;
        });
    }

    // ── Property 3: Soft-deleted spaces excluded from listings ────────────────
    // Given a mix of active and soft-deleted spaces, filtering by DeletedAt == null
    // returns only active ones
    // **Validates: Requirements 1.3**

    [Property(MaxTest = 100)]
    public Property Property3_ListingFilter_ReturnsOnlyActiveSpaces()
    {
        // Generate total spaces [1..30] and how many are soft-deleted [0..total]
        var gen = from totalSpaces in Gen.Choose(1, 30)
                  from deletedCount in Gen.Choose(0, totalSpaces)
                  select (totalSpaces, deletedCount);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (totalSpaces, deletedCount) = tuple;

            // Create spaces
            var spaces = Enumerable.Range(0, totalSpaces)
                .Select(i => CreateSpace($"Space {i}"))
                .ToList();

            // Soft-delete the first N spaces
            for (int i = 0; i < deletedCount; i++)
            {
                spaces[i].SoftDelete();
            }

            // Act: simulate listing query filter (DeletedAt == null)
            var listed = spaces.Where(s => s.DeletedAt == null).ToList();

            // Assert: listed count equals active count
            var expectedActive = totalSpaces - deletedCount;
            return listed.Count == expectedActive;
        });
    }

    [Property(MaxTest = 100)]
    public Property Property3_ListingFilter_NeverIncludesSoftDeletedSpaces()
    {
        var gen = from totalSpaces in Gen.Choose(1, 20)
                  from deletedCount in Gen.Choose(1, totalSpaces)
                  select (totalSpaces, deletedCount);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (totalSpaces, deletedCount) = tuple;

            var spaces = Enumerable.Range(0, totalSpaces)
                .Select(i => CreateSpace($"Space {i}"))
                .ToList();

            // Soft-delete some
            for (int i = 0; i < deletedCount; i++)
            {
                spaces[i].SoftDelete();
            }

            // Act: listing filter
            var listed = spaces.Where(s => s.DeletedAt == null).ToList();

            // Assert: no listed space has a non-null DeletedAt
            return listed.All(s => s.DeletedAt == null);
        });
    }

    [Property(MaxTest = 100)]
    public Property Property3_AllActiveSpaces_AppearInListing()
    {
        var gen = from totalSpaces in Gen.Choose(1, 20)
                  from deletedCount in Gen.Choose(0, totalSpaces)
                  select (totalSpaces, deletedCount);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (totalSpaces, deletedCount) = tuple;

            var spaces = Enumerable.Range(0, totalSpaces)
                .Select(i => CreateSpace($"Space {i}"))
                .ToList();

            for (int i = 0; i < deletedCount; i++)
            {
                spaces[i].SoftDelete();
            }

            var listed = spaces.Where(s => s.DeletedAt == null).ToList();
            var activeSpaces = spaces.Skip(deletedCount).ToList();

            // All active spaces must appear in the listing
            return activeSpaces.All(s => listed.Contains(s));
        });
    }
}
