// Feature: home-leave-scheduling
// Property 11: Template name validation
// Validates: Requirements 10.8

using FluentAssertions;
using Jobuler.Application.HomeLeave.Commands;
using Jobuler.Application.HomeLeave.Validators;
using Xunit;

namespace Jobuler.Tests.HomeLeave;

public class HomeLeaveTemplateNamePropertyTests
{
    private readonly CreateHomeLeaveTemplateCommandValidator _validator = new();

    /// <summary>
    /// Determines whether a given string should be accepted as a template name.
    /// Oracle function implementing the specification:
    /// - Trimmed length between 1 and 100 characters inclusive
    /// - No leading or trailing whitespace in the original string
    /// </summary>
    private static bool ShouldBeValidName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        var trimmed = name.Trim();

        // No leading/trailing whitespace (original must equal trimmed)
        if (name != trimmed)
            return false;

        // Trimmed length must be 1–100
        return trimmed.Length >= 1 && trimmed.Length <= 100;
    }

    /// <summary>
    /// Creates a command with valid config values so only the name validation is tested.
    /// </summary>
    private static CreateHomeLeaveTemplateCommand MakeCommand(string name)
    {
        return new CreateHomeLeaveTemplateCommand(
            SpaceId: Guid.NewGuid(),
            Name: name,
            MinRestHours: 8,
            EligibilityThresholdHours: 24,
            LeaveCapacity: 1,
            LeaveDurationHours: 48,
            RequestingUserId: Guid.NewGuid());
    }

    /// <summary>
    /// Checks if the validation result has a name-specific error (not other field errors).
    /// </summary>
    private bool IsNameValid(string name)
    {
        var cmd = MakeCommand(name);
        var result = _validator.Validate(cmd);

        // Check if there are any errors specifically for the Name property
        var nameErrors = result.Errors.Where(e => e.PropertyName == "Name").ToList();
        return nameErrors.Count == 0;
    }

    // ── Property 11: Template name validation ──
    // Feature: home-leave-scheduling, Property 11: template name validation

    [Fact]
    public void Property11_RandomStrings_ValidationMatchesSpecification()
    {
        // **Validates: Requirements 10.8**
        // Generate diverse random strings and verify the validator matches the oracle
        var rng = new Random(42);
        var validCount = 0;
        var invalidCount = 0;

        for (int i = 0; i < 200; i++)
        {
            var name = GenerateRandomString(rng);
            var expected = ShouldBeValidName(name);
            var actual = IsNameValid(name);

            actual.Should().Be(expected,
                because: $"name \"{Escape(name)}\" (len={name.Length}) should be " +
                         $"{(expected ? "valid" : "invalid")} per spec");

            if (expected) validCount++;
            else invalidCount++;
        }

        // Ensure we tested both valid and invalid cases
        validCount.Should().BeGreaterThan(0, "should have tested at least one valid name");
        invalidCount.Should().BeGreaterThan(0, "should have tested at least one invalid name");
    }

    // ── Valid names: no whitespace issues, length 1–100 ──

    [Theory]
    [InlineData("A")]                          // minimum length
    [InlineData("Template 1")]                 // typical name
    [InlineData("תבנית בסיסית")]              // Hebrew characters
    [InlineData("My-Template_v2.0")]           // special chars
    [InlineData("a b c")]                      // internal spaces are fine
    public void Property11_ValidNames_Accepted(string name)
    {
        // **Validates: Requirements 10.8**
        IsNameValid(name).Should().BeTrue($"'{name}' should be a valid template name");
    }

    [Fact]
    public void Property11_MaxLengthName_Accepted()
    {
        // **Validates: Requirements 10.8**
        var name = new string('x', 100); // exactly 100 chars
        IsNameValid(name).Should().BeTrue("100-char name should be valid");
    }

    // ── Invalid names: leading/trailing whitespace ──

    [Theory]
    [InlineData(" leading")]           // leading space
    [InlineData("trailing ")]          // trailing space
    [InlineData(" both ")]             // both
    [InlineData("\tleading tab")]      // leading tab
    [InlineData("trailing tab\t")]     // trailing tab
    [InlineData("\nleading newline")]  // leading newline
    [InlineData("trailing newline\n")] // trailing newline
    public void Property11_LeadingTrailingWhitespace_Rejected(string name)
    {
        // **Validates: Requirements 10.8**
        IsNameValid(name).Should().BeFalse($"'{Escape(name)}' has leading/trailing whitespace");
    }

    // ── Invalid names: empty or too long ──

    [Theory]
    [InlineData("")]     // empty
    [InlineData("   ")]  // whitespace only (also has leading/trailing whitespace)
    public void Property11_EmptyOrWhitespaceOnly_Rejected(string name)
    {
        // **Validates: Requirements 10.8**
        IsNameValid(name).Should().BeFalse($"'{Escape(name)}' should be rejected");
    }

    [Fact]
    public void Property11_TooLongName_Rejected()
    {
        // **Validates: Requirements 10.8**
        var name = new string('x', 101); // 101 chars exceeds max
        IsNameValid(name).Should().BeFalse("101-char name should be rejected");
    }

    // ── Boundary: exactly at length limits ──

    [Theory]
    [InlineData(1, true)]    // minimum valid length
    [InlineData(50, true)]   // middle
    [InlineData(99, true)]   // just below max
    [InlineData(100, true)]  // at max (valid)
    [InlineData(101, false)] // just above max (invalid)
    [InlineData(150, false)] // well above max
    public void Property11_LengthBoundaries(int length, bool expectedValid)
    {
        // **Validates: Requirements 10.8**
        var name = new string('a', length);
        IsNameValid(name).Should().Be(expectedValid,
            $"name of length {length} should be {(expectedValid ? "valid" : "invalid")}");
    }

    // ── Helpers ──

    private static string GenerateRandomString(Random rng)
    {
        // Decide what kind of string to generate
        var kind = rng.Next(10);

        return kind switch
        {
            0 => "",                                          // empty
            1 => " " + RandomChars(rng, rng.Next(1, 50)),    // leading space
            2 => RandomChars(rng, rng.Next(1, 50)) + " ",    // trailing space
            3 => " " + RandomChars(rng, rng.Next(1, 50)) + " ", // both
            4 => new string('x', rng.Next(101, 200)),        // too long
            5 => new string(' ', rng.Next(1, 10)),           // whitespace only
            6 => RandomChars(rng, 1),                        // single char (valid)
            7 => RandomChars(rng, 100),                      // exactly 100 (valid)
            8 => RandomChars(rng, rng.Next(1, 100)),         // valid length, no whitespace issues
            _ => RandomChars(rng, rng.Next(2, 80)),          // valid length, no whitespace issues
        };
    }

    private static string RandomChars(Random rng, int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_. אבגדהוזחטי";
        var result = new char[length];
        for (int i = 0; i < length; i++)
            result[i] = chars[rng.Next(chars.Length)];
        return new string(result);
    }

    private static string Escape(string s)
    {
        return s.Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r");
    }
}
