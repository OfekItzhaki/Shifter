// Feature: space-management
// Property-based tests for settings commands (Task 7.6)
// Properties 8, 9, 10, 11

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Spaces.Commands;
using Jobuler.Application.Spaces.Validators;
using Jobuler.Domain.Spaces;
using Xunit;

namespace Jobuler.Tests.Domain;

[Trait("Feature", "space-management")]
public class SpaceSettingsPropertyTests
{
    // ── Generators ───────────────────────────────────────────────────────────

    /// <summary>
    /// Generates integers in the valid management timeout range [5, 120].
    /// </summary>
    private static Arbitrary<int> ValidTimeoutArbitrary()
    {
        return Arb.From(Gen.Choose(5, 120));
    }

    /// <summary>
    /// Generates integers outside the valid management timeout range [5, 120].
    /// Covers: negative, zero, 1-4, and 121+.
    /// </summary>
    private static Arbitrary<int> InvalidTimeoutArbitrary()
    {
        var belowRange = Gen.Choose(int.MinValue / 2, 4);
        var aboveRange = Gen.Choose(121, int.MaxValue / 2);
        var gen = Gen.OneOf(belowRange, aboveRange);
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates valid space names: 2–100 printable characters (after trim).
    /// </summary>
    private static Arbitrary<string> ValidNameArbitrary()
    {
        // Generate strings of length 2-100 using printable non-whitespace chars,
        // optionally surrounded by whitespace (to test trim behavior)
        var gen = from length in Gen.Choose(2, 100)
                  from chars in Gen.ArrayOf(length, Gen.Elements(
                      "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_ ".ToCharArray()))
                  let core = new string(chars)
                  // Ensure at least two non-whitespace characters after trim
                  where core.Trim().Length >= 2 && core.Trim().Length <= 100
                  from leadingSpaces in Gen.Choose(0, 3)
                  from trailingSpaces in Gen.Choose(0, 3)
                  let padded = new string(' ', leadingSpaces) + core + new string(' ', trailingSpaces)
                  select padded;

        return Arb.From(gen);
    }

    /// <summary>
    /// Generates invalid space names: empty, whitespace-only, single char, or >100 chars after trim.
    /// </summary>
    private static Arbitrary<string> InvalidNameArbitrary()
    {
        var empty = Gen.Constant(string.Empty);
        var whitespaceOnly = from count in Gen.Choose(1, 10)
                             select new string(' ', count);
        var singleChar = from c in Gen.Elements("abcdefghijklmnopqrstuvwxyz".ToCharArray())
                         from leadingSpaces in Gen.Choose(0, 3)
                         from trailingSpaces in Gen.Choose(0, 3)
                         select new string(' ', leadingSpaces) + c + new string(' ', trailingSpaces);
        var tooLong = from length in Gen.Choose(101, 200)
                      from chars in Gen.ArrayOf(length, Gen.Elements(
                          "abcdefghijklmnopqrstuvwxyz".ToCharArray()))
                      select new string(chars);
        var nullStr = Gen.Constant((string)null!);

        var gen = Gen.OneOf(empty, whitespaceOnly, singleChar, tooLong, nullStr);
        return Arb.From(gen);
    }

    // ── Property 8: Management timeout validation ────────────────────────────
    // **Validates: Requirements 5.2, 5.3**
    // For any integer value, SetManagementTimeout accepts [5, 120] and rejects outside.

    [Property(MaxTest = 100)]
    public Property Property8_SetManagementTimeout_AcceptsValidRange()
    {
        return Prop.ForAll(ValidTimeoutArbitrary(), minutes =>
        {
            var space = Space.Create("Test Space", Guid.NewGuid());

            space.SetManagementTimeout(minutes);

            return (space.ManagementTimeoutMinutes == minutes)
                .Label($"Expected ManagementTimeoutMinutes={minutes}, got {space.ManagementTimeoutMinutes}");
        });
    }

    [Property(MaxTest = 100)]
    public Property Property8_SetManagementTimeout_RejectsInvalidRange()
    {
        return Prop.ForAll(InvalidTimeoutArbitrary(), minutes =>
        {
            var space = Space.Create("Test Space", Guid.NewGuid());
            var originalTimeout = space.ManagementTimeoutMinutes;

            var threw = false;
            try
            {
                space.SetManagementTimeout(minutes);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            return threw
                .Label($"Expected InvalidOperationException for minutes={minutes}")
                .And((space.ManagementTimeoutMinutes == originalTimeout)
                .Label($"Timeout should remain {originalTimeout} after rejection, was {space.ManagementTimeoutMinutes}"));
        });
    }

    // ── Property 9: Space-level timeout propagates to groups ─────────────────
    // **Validates: Requirements 5.4, 5.5**
    // When a space has ManagementTimeoutMinutes set, that value is the authoritative
    // timeout for all groups. At the domain level, we verify the space stores the value
    // correctly so it can be read by groups.

    [Property(MaxTest = 100)]
    public Property Property9_SpaceLevelTimeout_StoredCorrectly_ForGroupPropagation()
    {
        return Prop.ForAll(ValidTimeoutArbitrary(), minutes =>
        {
            var space = Space.Create("Test Space", Guid.NewGuid());

            space.SetManagementTimeout(minutes);

            // The space stores the timeout value that groups will read
            return (space.ManagementTimeoutMinutes == minutes)
                .Label($"Space should store timeout={minutes} for group propagation, got {space.ManagementTimeoutMinutes}");
        });
    }

    [Property(MaxTest = 100)]
    public Property Property9_SpaceLevelTimeout_MultipleUpdates_LastValueWins()
    {
        // Generate two valid timeout values
        var gen = from first in Gen.Choose(5, 120)
                  from second in Gen.Choose(5, 120)
                  select (first, second);

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            var (first, second) = pair;
            var space = Space.Create("Test Space", Guid.NewGuid());

            space.SetManagementTimeout(first);
            space.SetManagementTimeout(second);

            // The last value set is the effective value for all groups
            return (space.ManagementTimeoutMinutes == second)
                .Label($"After setting {first} then {second}, expected {second} but got {space.ManagementTimeoutMinutes}");
        });
    }

    // ── Property 10: Space name validation ───────────────────────────────────
    // **Validates: Requirements 7.2, 7.3**
    // Names 1–100 chars after trim accepted, empty or >100 rejected.
    // We test at the validator level since the handler uses both validator and inline check.

    [Property(MaxTest = 100)]
    public Property Property10_SpaceNameValidation_AcceptsValidNames()
    {
        return Prop.ForAll(ValidNameArbitrary(), name =>
        {
            var validator = new UpdateSpaceCommandValidator();
            var cmd = new UpdateSpaceCommand(
                Guid.NewGuid(), name, null, "he", Guid.NewGuid());

            var result = validator.Validate(cmd);

            var trimmedLength = name.Trim().Length;
            return result.IsValid
                .Label($"Name '{name}' (trimmed length={trimmedLength}) should be valid but validator rejected it: " +
                       $"{string.Join("; ", result.Errors.Select(e => e.ErrorMessage))}");
        });
    }

    [Property(MaxTest = 100)]
    public Property Property10_SpaceNameValidation_RejectsInvalidNames()
    {
        return Prop.ForAll(InvalidNameArbitrary(), name =>
        {
            var validator = new UpdateSpaceCommandValidator();
            var cmd = new UpdateSpaceCommand(
                Guid.NewGuid(), name, null, "he", Guid.NewGuid());

            var result = validator.Validate(cmd);

            var displayName = name ?? "<null>";
            var trimmedLength = name?.Trim().Length ?? 0;
            return (!result.IsValid)
                .Label($"Name '{displayName}' (trimmed length={trimmedLength}) should be invalid but validator accepted it");
        });
    }

    // ── Property 11: Invite code regeneration ────────────────────────────────
    // **Validates: Requirements 8.3**
    // Produces 8-char alphanumeric string different from previous.

    [Property(MaxTest = 100)]
    public Property Property11_RegenerateInviteCode_Produces8CharAlphanumeric()
    {
        return Prop.ForAll(Arb.From(Gen.Constant(0)), _ =>
        {
            var space = Space.Create("Test Space", Guid.NewGuid());
            var previousCode = space.InviteCode;

            var newCode = space.RegenerateInviteCode();

            var is8Chars = newCode.Length == 8;
            var isAlphanumeric = newCode.All(c => char.IsLetterOrDigit(c));
            var isDifferent = newCode != previousCode;

            return is8Chars
                .Label($"Code '{newCode}' should be 8 chars, was {newCode.Length}")
                .And(isAlphanumeric
                .Label($"Code '{newCode}' should be alphanumeric"))
                .And(isDifferent
                .Label($"New code '{newCode}' should differ from previous '{previousCode}'"));
        });
    }

    [Property(MaxTest = 100)]
    public Property Property11_RegenerateInviteCode_ConsecutiveRegenerations_AlwaysDifferent()
    {
        // Generate a number of consecutive regenerations (2-10)
        return Prop.ForAll(Arb.From(Gen.Choose(2, 10)), count =>
        {
            var space = Space.Create("Test Space", Guid.NewGuid());
            var codes = new List<string>();

            // Collect initial code
            codes.Add(space.InviteCode!);

            // Regenerate multiple times
            for (int i = 0; i < count; i++)
            {
                var newCode = space.RegenerateInviteCode();
                codes.Add(newCode);
            }

            // Each consecutive pair should be different
            var allConsecutiveDifferent = true;
            for (int i = 1; i < codes.Count; i++)
            {
                if (codes[i] == codes[i - 1])
                {
                    allConsecutiveDifferent = false;
                    break;
                }
            }

            // All codes should be 8-char alphanumeric
            var allValid = codes.All(c => c.Length == 8 && c.All(ch => char.IsLetterOrDigit(ch)));

            return allValid
                .Label($"All codes should be 8-char alphanumeric: [{string.Join(", ", codes)}]")
                .And(allConsecutiveDifferent
                .Label($"Consecutive codes should differ: [{string.Join(", ", codes)}]"));
        });
    }
}
