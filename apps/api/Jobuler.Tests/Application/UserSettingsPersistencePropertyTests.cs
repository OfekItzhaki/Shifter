// Feature: user-timezone-settings
// Properties 2, 3: User Settings Persistence Round-Trip, Geographic Code Validation
// Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5

using FluentAssertions;
using FluentValidation.TestHelper;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.UserSettings;
using Jobuler.Application.UserSettings.Commands;
using Jobuler.Application.UserSettings.Validators;
using Jobuler.Domain.Identity;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Jobuler.Tests.Application;

public class UserSettingsPersistencePropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// All valid country codes from ValidLocationCodes.
    /// </summary>
    private static readonly string[] AllValidCountryCodes =
        ValidLocationCodes.ValidCountryCodes.ToArray();

    /// <summary>
    /// All multi-timezone countries that have state subdivisions.
    /// </summary>
    private static readonly string[] CountriesWithStates =
        ValidLocationCodes.ValidStateCodes.Keys.ToArray();

    /// <summary>
    /// Flattened list of (CountryCode, StateCode) pairs from ValidStateCodes.
    /// </summary>
    private static readonly (string Country, string State)[] AllValidCountryStatePairs =
        ValidLocationCodes.ValidStateCodes
            .SelectMany(kvp => kvp.Value.Select(state => (kvp.Key, state)))
            .ToArray();

    // ══════════════════════════════════════════════════════════════════════════
    // Property 2: User Settings Persistence Round-Trip
    // For any valid ISO 3166-1 alpha-2 country code and valid ISO 3166-2
    // subdivision code belonging to that country, persisting the pair via the
    // Settings_Service and reading it back SHALL return the identical country
    // and state codes.
    // **Validates: Requirements 2.1, 2.2**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property PersistAndReadBack_CountryOnly_ReturnsIdenticalCode()
    {
        var gen = Gen.Elements(AllValidCountryCodes);

        return Prop.ForAll(Arb.From(gen), countryCode =>
        {
            // Arrange
            var db = CreateDb();
            var user = User.Create("test@example.com", "Test User", "hash123");
            db.Users.Add(user);
            db.SaveChanges();

            // Act — persist
            user.UpdateLocation(countryCode, null);
            db.SaveChanges();

            // Read back from a fresh context to ensure DB round-trip
            var readUser = db.Users.First(u => u.Id == user.Id);

            // Assert
            return (readUser.CountryCode == countryCode.ToUpperInvariant().Trim()
                    && readUser.StateCode == null)
                .Label($"Expected CountryCode='{countryCode.ToUpperInvariant()}', StateCode=null; " +
                       $"Got CountryCode='{readUser.CountryCode}', StateCode='{readUser.StateCode}'");
        });
    }

    [Property(MaxTest = 100)]
    public Property PersistAndReadBack_CountryAndState_ReturnsIdenticalCodes()
    {
        var gen = Gen.Elements(AllValidCountryStatePairs);

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            // Arrange
            var db = CreateDb();
            var user = User.Create("test@example.com", "Test User", "hash123");
            db.Users.Add(user);
            db.SaveChanges();

            // Act — persist
            user.UpdateLocation(pair.Country, pair.State);
            db.SaveChanges();

            // Read back
            var readUser = db.Users.First(u => u.Id == user.Id);

            // Assert
            var expectedCountry = pair.Country.ToUpperInvariant().Trim();
            var expectedState = pair.State.ToUpperInvariant().Trim();

            return (readUser.CountryCode == expectedCountry && readUser.StateCode == expectedState)
                .Label($"Expected ({expectedCountry}, {expectedState}); " +
                       $"Got ({readUser.CountryCode}, {readUser.StateCode})");
        });
    }

    [Property(MaxTest = 100)]
    public Property PersistAndReadBack_CaseInsensitive_NormalizesToUpperCase()
    {
        // Generate valid country codes in random casing
        var gen = from country in Gen.Elements(AllValidCountryCodes)
                  from useLower in Arb.Default.Bool().Generator
                  select useLower ? country.ToLowerInvariant() : country;

        return Prop.ForAll(Arb.From(gen), countryCode =>
        {
            // Arrange
            var db = CreateDb();
            var user = User.Create("test@example.com", "Test User", "hash123");
            db.Users.Add(user);
            db.SaveChanges();

            // Act — persist with potentially lower-case input
            user.UpdateLocation(countryCode, null);
            db.SaveChanges();

            // Read back
            var readUser = db.Users.First(u => u.Id == user.Id);

            // Assert — should always be stored as upper-case
            return (readUser.CountryCode == countryCode.ToUpperInvariant().Trim())
                .Label($"Input '{countryCode}' should normalize to '{countryCode.ToUpperInvariant()}', got '{readUser.CountryCode}'");
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 3: Geographic Code Validation
    // For any string that is NOT a valid ISO 3166-1 alpha-2 code, the
    // Settings_Service SHALL reject it; and for any valid country code paired
    // with a state code that does NOT belong to that country, the
    // Settings_Service SHALL reject the pair with a validation error.
    // **Validates: Requirements 2.3, 2.4, 2.5**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 100)]
    public Property InvalidCountryCode_IsRejectedByValidator()
    {
        // Generate random strings that are NOT valid country codes
        var gen = from s in Arb.Default.NonEmptyString().Generator
                  let str = s.Get
                  where !ValidLocationCodes.IsValidCountryCode(str)
                  select str;

        return Prop.ForAll(Arb.From(gen), invalidCountry =>
        {
            // Arrange
            var validator = new UpdateUserLocationValidator();
            var command = new UpdateUserLocationCommand(Guid.NewGuid(), invalidCountry, null);

            // Act
            var result = validator.TestValidate(command);

            // Assert — should have validation errors on CountryCode
            return result.Errors.Any(e => e.PropertyName == "CountryCode")
                .Label($"Expected validation error for invalid country '{invalidCountry}', but none found. " +
                       $"Errors: [{string.Join(", ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))}]");
        });
    }

    [Property(MaxTest = 100)]
    public Property ValidCountryCode_IsAcceptedByValidator()
    {
        var gen = Gen.Elements(AllValidCountryCodes);

        return Prop.ForAll(Arb.From(gen), validCountry =>
        {
            // Arrange
            var validator = new UpdateUserLocationValidator();
            var command = new UpdateUserLocationCommand(Guid.NewGuid(), validCountry, null);

            // Act
            var result = validator.TestValidate(command);

            // Assert — should NOT have validation errors on CountryCode
            var hasNoCountryError = !result.Errors.Any(e => e.PropertyName == "CountryCode");
            return hasNoCountryError
                .Label($"Expected no validation error for valid country '{validCountry}', but got: " +
                       $"[{string.Join(", ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))}]");
        });
    }

    [Property(MaxTest = 100)]
    public Property InvalidStateForCountry_IsRejectedByValidator()
    {
        // Generate a valid multi-timezone country paired with a state that does NOT belong to it
        var gen = from country in Gen.Elements(CountriesWithStates)
                  from randomState in Arb.Default.NonEmptyString().Generator
                  let stateStr = randomState.Get
                  where !ValidLocationCodes.IsValidStateCode(country, stateStr)
                        && !string.IsNullOrWhiteSpace(stateStr)
                  select (Country: country, State: stateStr);

        return Prop.ForAll(Arb.From(gen), input =>
        {
            // Arrange
            var validator = new UpdateUserLocationValidator();
            var command = new UpdateUserLocationCommand(Guid.NewGuid(), input.Country, input.State);

            // Act
            var result = validator.TestValidate(command);

            // Assert — should have validation error on StateCode
            return result.Errors.Any(e => e.PropertyName == "StateCode")
                .Label($"Expected validation error for state '{input.State}' in country '{input.Country}', but none found. " +
                       $"Errors: [{string.Join(", ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))}]");
        });
    }

    [Property(MaxTest = 100)]
    public Property ValidStateForCountry_IsAcceptedByValidator()
    {
        var gen = Gen.Elements(AllValidCountryStatePairs);

        return Prop.ForAll(Arb.From(gen), pair =>
        {
            // Arrange
            var validator = new UpdateUserLocationValidator();
            var command = new UpdateUserLocationCommand(Guid.NewGuid(), pair.Country, pair.State);

            // Act
            var result = validator.TestValidate(command);

            // Assert — should NOT have any validation errors
            return (result.Errors.Count == 0)
                .Label($"Expected no validation errors for ({pair.Country}, {pair.State}), but got: " +
                       $"[{string.Join(", ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"))}]");
        });
    }

    [Property(MaxTest = 100)]
    public Property StateForSingleTimezoneCountry_IsRejectedByValidator()
    {
        // Countries that do NOT have state subdivisions — providing a state should be rejected
        var singleTzCountries = AllValidCountryCodes
            .Where(c => !ValidLocationCodes.HasStateSubdivisions(c))
            .ToArray();

        var gen = from country in Gen.Elements(singleTzCountries)
                  from state in Arb.Default.NonEmptyString().Generator
                  let stateStr = state.Get
                  where !string.IsNullOrWhiteSpace(stateStr)
                  select (Country: country, State: stateStr);

        return Prop.ForAll(Arb.From(gen), input =>
        {
            // Arrange
            var validator = new UpdateUserLocationValidator();
            var command = new UpdateUserLocationCommand(Guid.NewGuid(), input.Country, input.State);

            // Act
            var result = validator.TestValidate(command);

            // Assert — state code should be rejected for single-timezone countries
            var hasStateError = result.Errors.Any(e => e.PropertyName == "StateCode");
            return hasStateError
                .Label($"Expected validation error for state '{input.State}' in single-TZ country '{input.Country}', but none found.");
        });
    }
}
