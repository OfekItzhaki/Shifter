using Jobuler.Application.Common;

namespace Jobuler.Infrastructure.Timezone;

/// <summary>
/// Resolves timezone from Country/State using a static mapping dictionary.
/// Fallback chain: State → Country (most populous TZ) → Asia/Jerusalem.
/// </summary>
public class TimezoneResolver : ITimezoneResolver
{
    private const string DefaultTimezone = "Asia/Jerusalem";

    public TimezoneResolution Resolve(string? countryCode, string? stateCode)
    {
        var ianaId = ResolveTimezoneId(countryCode?.ToUpperInvariant()?.Trim(),
                                        stateCode?.ToUpperInvariant()?.Trim());
        var offsetMinutes = ComputeOffsetMinutes(ianaId);
        return new TimezoneResolution(ianaId, offsetMinutes);
    }

    private static string ResolveTimezoneId(string? countryCode, string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return DefaultTimezone;

        // Try state-level resolution first (for multi-timezone countries)
        if (!string.IsNullOrWhiteSpace(stateCode))
        {
            var stateKey = $"{countryCode}-{stateCode}";
            if (CountryTimezoneMap.StateMappings.TryGetValue(stateKey, out var stateTz))
                return stateTz;
        }

        // Fall back to country-level (most populous timezone for multi-TZ countries)
        if (CountryTimezoneMap.CountryMappings.TryGetValue(countryCode, out var countryTz))
            return countryTz;

        // Ultimate fallback
        return DefaultTimezone;
    }

    private static int ComputeOffsetMinutes(string ianaTimezoneId)
    {
        try
        {
            var tzInfo = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezoneId);
            var offset = tzInfo.GetUtcOffset(DateTimeOffset.UtcNow);
            return (int)offset.TotalMinutes;
        }
        catch (TimeZoneNotFoundException)
        {
            // If the IANA ID is not recognized on this system, try Windows ID mapping
            try
            {
                var tzInfo = FindTimeZoneByIana(ianaTimezoneId);
                if (tzInfo != null)
                {
                    var offset = tzInfo.GetUtcOffset(DateTimeOffset.UtcNow);
                    return (int)offset.TotalMinutes;
                }
            }
            catch
            {
                // Fall through to default
            }

            // Default to Asia/Jerusalem offset if all else fails
            var jerusalemTz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
            return (int)jerusalemTz.GetUtcOffset(DateTimeOffset.UtcNow).TotalMinutes;
        }
    }

    private static TimeZoneInfo? FindTimeZoneByIana(string ianaId)
    {
        // .NET 6+ supports IANA IDs on all platforms via TimeZoneInfo.FindSystemTimeZoneById
        // This fallback handles edge cases where the ID might need Windows mapping
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(ianaId, out var windowsId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
        return null;
    }
}
