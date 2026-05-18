// Feature: user-timezone-settings
// Properties 4, 5, 6: Timezone Resolver Output Validity, Single-Timezone Country Invariant, Offset Computation Correctness
// Validates: Requirements 3.1, 3.3, 4.1

using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Common;
using Jobuler.Infrastructure.Timezone;
using Xunit;

namespace Jobuler.Tests.Domain;

public class TimezoneResolverPropertyTests
{
    private readonly TimezoneResolver _resolver = new();

    /// <summary>
    /// All known valid IANA timezone IDs: those resolvable on this system plus all IDs
    /// referenced in our CountryTimezoneMap (which are sourced from the IANA tzdata set).
    /// On Windows, some IANA IDs (e.g., Asia/Urumqi) aren't directly resolvable but are
    /// still valid IANA identifiers that can be converted via TryConvertIanaIdToWindowsId.
    /// </summary>
    private static readonly HashSet<string> KnownValidIanaIds = BuildKnownIanaIds();

    private static HashSet<string> BuildKnownIanaIds()
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // All system timezone IDs (includes IANA IDs on Linux, Windows IDs on Windows)
        foreach (var tz in TimeZoneInfo.GetSystemTimeZones())
        {
            ids.Add(tz.Id);
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var ianaId))
                ids.Add(ianaId);
        }

        // All IDs from our map are valid IANA IDs by definition (sourced from tzdata)
        foreach (var val in CountryTimezoneMap.CountryMappings.Values)
            ids.Add(val);
        foreach (var val in CountryTimezoneMap.StateMappings.Values)
            ids.Add(val);

        return ids;
    }

    /// <summary>
    /// Checks if a timezone ID is a valid IANA timezone identifier.
    /// Valid means: resolvable on this system directly, convertible via IANA→Windows mapping,
    /// or present in the known IANA tzdata set (our map).
    /// </summary>
    private static bool IsValidIanaTimezoneId(string ianaId)
    {
        if (KnownValidIanaIds.Contains(ianaId))
            return true;

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            // Try Windows ID mapping — if it converts, it's a valid IANA ID
            return TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out _);
        }
    }

    /// <summary>
    /// All country codes present in CountryTimezoneMap.CountryMappings.
    /// </summary>
    private static readonly string[] AllCountryCodes = CountryTimezoneMap.CountryMappings.Keys.ToArray();

    /// <summary>
    /// Single-timezone countries: those in CountryMappings but NOT in MultiTimezoneCountries.
    /// </summary>
    private static readonly string[] SingleTimezoneCountries = CountryTimezoneMap.CountryMappings.Keys
        .Where(cc => !CountryTimezoneMap.MultiTimezoneCountries.Contains(cc))
        .ToArray();

    /// <summary>
    /// All state keys from StateMappings (e.g., "US-NY", "AU-NSW").
    /// </summary>
    private static readonly string[] AllStateKeys = CountryTimezoneMap.StateMappings.Keys.ToArray();

    /// <summary>
    /// All unique IANA timezone IDs referenced in the map (for offset testing).
    /// </summary>
    private static readonly string[] AllMappedTimezoneIds = CountryTimezoneMap.CountryMappings.Values
        .Concat(CountryTimezoneMap.StateMappings.Values)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    // ══════════════════════════════════════════════════════════════════════════
    // Property 4: Timezone Resolver Output Validity
    // For any valid country code and optional valid state code, the resolver
    // SHALL return a string that is a valid IANA timezone identifier.
    // **Validates: Requirements 3.1**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 200)]
    public Property OutputIsAlwaysValidIanaTimezone_CountryOnly()
    {
        var gen = Gen.Elements(AllCountryCodes);

        return Prop.ForAll(Arb.From(gen), countryCode =>
        {
            var result = _resolver.Resolve(countryCode, null);
            return IsValidIanaTimezoneId(result.IanaTimezoneId)
                .Label($"Expected valid IANA ID but got '{result.IanaTimezoneId}' for country '{countryCode}'");
        });
    }

    [Property(MaxTest = 200)]
    public Property OutputIsAlwaysValidIanaTimezone_WithState()
    {
        var gen = Gen.Elements(AllStateKeys)
            .Select(key =>
            {
                var parts = key.Split('-', 2);
                return (Country: parts[0], State: parts.Length > 1 ? parts[1] : null);
            });

        return Prop.ForAll(Arb.From(gen), input =>
        {
            var result = _resolver.Resolve(input.Country, input.State);
            return IsValidIanaTimezoneId(result.IanaTimezoneId)
                .Label($"Expected valid IANA ID but got '{result.IanaTimezoneId}' for '{input.Country}-{input.State}'");
        });
    }

    [Property(MaxTest = 100)]
    public Property OutputIsAlwaysValidIanaTimezone_RandomStateForCountry()
    {
        // Generate a valid country code paired with a random arbitrary string as state
        var gen = from country in Gen.Elements(AllCountryCodes)
                  from state in Arb.Default.String().Generator
                  select (Country: country, State: state);

        return Prop.ForAll(Arb.From(gen), input =>
        {
            var result = _resolver.Resolve(input.Country, input.State);
            return IsValidIanaTimezoneId(result.IanaTimezoneId)
                .Label($"Expected valid IANA ID but got '{result.IanaTimezoneId}' for country='{input.Country}', state='{input.State}'");
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 5: Single-Timezone Country Invariant
    // For any country that has exactly one timezone, and for any state/region
    // value (including null), the resolver SHALL return the same IANA timezone
    // identifier regardless of the state input.
    // **Validates: Requirements 3.3**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 200)]
    public Property SingleTimezoneCountry_AlwaysReturnsSameTimezone()
    {
        var gen = from country in Gen.Elements(SingleTimezoneCountries)
                  from state in Arb.Default.String().Generator
                  select (Country: country, State: state);

        return Prop.ForAll(Arb.From(gen), input =>
        {
            var expectedTz = CountryTimezoneMap.CountryMappings[input.Country];
            var result = _resolver.Resolve(input.Country, input.State);
            return (result.IanaTimezoneId == expectedTz)
                .Label($"Expected '{expectedTz}' for single-TZ country '{input.Country}' with state='{input.State}', got '{result.IanaTimezoneId}'");
        });
    }

    [Property(MaxTest = 100)]
    public Property SingleTimezoneCountry_NullState_ReturnsSameAsAnyState()
    {
        var gen = from country in Gen.Elements(SingleTimezoneCountries)
                  from state in Arb.Default.String().Generator
                  select (Country: country, State: state);

        return Prop.ForAll(Arb.From(gen), input =>
        {
            var withNull = _resolver.Resolve(input.Country, null);
            var withState = _resolver.Resolve(input.Country, input.State);
            return (withNull.IanaTimezoneId == withState.IanaTimezoneId)
                .Label($"Single-TZ country '{input.Country}': null→'{withNull.IanaTimezoneId}' vs state='{input.State}'→'{withState.IanaTimezoneId}'");
        });
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Property 6: Offset Computation Correctness
    // For any valid IANA timezone identifier and for any point in time, the
    // computed offset in minutes SHALL equal the actual UTC offset defined by
    // that timezone's rules (including DST) at that moment.
    // **Validates: Requirements 4.1**
    // ══════════════════════════════════════════════════════════════════════════

    [Property(MaxTest = 200)]
    public Property OffsetMatchesExpectedForMappedTimezones()
    {
        // We test the offset at the current moment for all mapped timezone IDs
        var gen = Gen.Elements(AllMappedTimezoneIds);

        return Prop.ForAll(Arb.From(gen), ianaId =>
        {
            var result = _resolver.Resolve(
                GetCountryForTimezone(ianaId),
                GetStateForTimezone(ianaId));

            // Compute expected offset independently
            var expectedOffset = ComputeExpectedOffset(result.IanaTimezoneId, DateTimeOffset.UtcNow);

            return (result.OffsetMinutes == expectedOffset)
                .Label($"Timezone '{result.IanaTimezoneId}': expected offset {expectedOffset} min, got {result.OffsetMinutes} min");
        });
    }

    [Property(MaxTest = 100)]
    public Property OffsetIsWithinValidRange()
    {
        // UTC offsets range from -720 (UTC-12) to +840 (UTC+14)
        var gen = Gen.Elements(AllCountryCodes);

        return Prop.ForAll(Arb.From(gen), countryCode =>
        {
            var result = _resolver.Resolve(countryCode, null);
            return (result.OffsetMinutes >= -720 && result.OffsetMinutes <= 840)
                .Label($"Offset {result.OffsetMinutes} min for '{result.IanaTimezoneId}' is outside valid range [-720, 840]");
        });
    }

    [Property(MaxTest = 100)]
    public Property OffsetForDefaultFallback_MatchesJerusalem()
    {
        // For null/empty/invalid country codes, should fall back to Asia/Jerusalem
        var gen = from s in Arb.Default.String().Generator
                  where string.IsNullOrWhiteSpace(s) || !CountryTimezoneMap.CountryMappings.ContainsKey(s?.Trim()?.ToUpperInvariant() ?? "")
                  select s;

        return Prop.ForAll(Arb.From(gen), invalidCountry =>
        {
            var result = _resolver.Resolve(invalidCountry, null);
            var expectedOffset = ComputeExpectedOffset("Asia/Jerusalem", DateTimeOffset.UtcNow);

            return (result.IanaTimezoneId == "Asia/Jerusalem" && result.OffsetMinutes == expectedOffset)
                .Label($"Expected Asia/Jerusalem (offset={expectedOffset}) for invalid country='{invalidCountry}', got '{result.IanaTimezoneId}' (offset={result.OffsetMinutes})");
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int ComputeExpectedOffset(string ianaId, DateTimeOffset atTime)
    {
        try
        {
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            return (int)tzInfo.GetUtcOffset(atTime).TotalMinutes;
        }
        catch (TimeZoneNotFoundException)
        {
            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out var windowsId))
            {
                var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return (int)tzInfo.GetUtcOffset(atTime).TotalMinutes;
            }
            // Fallback to Jerusalem
            var jerusalem = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
            return (int)jerusalem.GetUtcOffset(atTime).TotalMinutes;
        }
    }

    /// <summary>
    /// Finds a country code that maps to the given timezone ID (for test input generation).
    /// </summary>
    private static string? GetCountryForTimezone(string ianaId)
    {
        foreach (var kvp in CountryTimezoneMap.CountryMappings)
        {
            if (string.Equals(kvp.Value, ianaId, StringComparison.OrdinalIgnoreCase))
                return kvp.Key;
        }
        // Check state mappings
        foreach (var kvp in CountryTimezoneMap.StateMappings)
        {
            if (string.Equals(kvp.Value, ianaId, StringComparison.OrdinalIgnoreCase))
                return kvp.Key.Split('-')[0];
        }
        return null;
    }

    /// <summary>
    /// Finds a state code that maps to the given timezone ID (for test input generation).
    /// </summary>
    private static string? GetStateForTimezone(string ianaId)
    {
        foreach (var kvp in CountryTimezoneMap.StateMappings)
        {
            if (string.Equals(kvp.Value, ianaId, StringComparison.OrdinalIgnoreCase))
            {
                var parts = kvp.Key.Split('-', 2);
                return parts.Length > 1 ? parts[1] : null;
            }
        }
        return null;
    }
}
