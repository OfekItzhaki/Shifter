namespace Jobuler.Application.Common;

/// <summary>
/// Resolves a user's timezone from their geographic location (Country/State).
/// Uses a fallback chain: State → Country (most populous TZ) → Asia/Jerusalem.
/// </summary>
public interface ITimezoneResolver
{
    TimezoneResolution Resolve(string? countryCode, string? stateCode);
}

/// <summary>
/// The result of timezone resolution containing the IANA timezone identifier
/// and the current UTC offset in minutes.
/// </summary>
public record TimezoneResolution(string IanaTimezoneId, int OffsetMinutes);
