namespace Jobuler.Application.UserSettings;

/// <summary>
/// Static reference data for ISO 3166-1 alpha-2 country codes and ISO 3166-2 subdivision codes
/// used for validation of user location input. This is a subset of ISO codes that the system
/// supports for timezone resolution.
/// 
/// Sources: ISO 3166-1 alpha-2, ISO 3166-2 subdivision codes.
/// Only countries/states that have timezone mappings are included.
/// </summary>
public static class ValidLocationCodes
{
    /// <summary>
    /// Set of valid ISO 3166-1 alpha-2 country codes supported by the system.
    /// </summary>
    public static IReadOnlySet<string> ValidCountryCodes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Multi-timezone countries
            "US", "RU", "AU", "CA", "BR", "MX", "ID", "CL",
            "KZ", "CN", "MN", "CD", "AR", "PT", "ES", "NZ",

            // Middle East
            "IL", "JO", "LB", "SY", "IQ", "KW", "SA", "BH",
            "QA", "AE", "OM", "YE", "IR",

            // Europe
            "GB", "IE", "IS", "FR", "DE", "IT", "NL", "BE",
            "LU", "CH", "AT", "PL", "CZ", "SK", "HU", "RO",
            "BG", "GR", "TR", "FI", "SE", "NO", "DK", "EE",
            "LV", "LT", "HR", "SI", "RS", "BA", "ME", "MK",
            "AL", "XK", "UA", "MD", "BY", "MT", "CY", "AD",
            "MC", "SM", "VA", "LI",

            // Asia
            "IN", "PK", "BD", "LK", "NP", "MM", "TH", "VN",
            "KH", "LA", "MY", "SG", "PH", "JP", "KR", "KP",
            "TW", "HK", "MO", "AF", "UZ", "TM", "TJ", "KG",
            "AZ", "GE", "AM", "BN", "TL", "BT", "MV",

            // Africa
            "EG", "ZA", "NG", "KE", "ET", "GH", "TZ", "UG",
            "MA", "DZ", "TN", "LY", "SD", "SS", "SN", "CI",
            "CM", "AO", "MZ", "ZW", "ZM", "BW", "NA", "MW",
            "RW", "MG", "MU",

            // Americas
            "CO", "VE", "PE", "EC", "BO", "PY", "UY", "GY",
            "SR", "PA", "CR", "NI", "HN", "SV", "GT", "BZ",
            "CU", "JM", "HT", "DO", "TT", "BB", "BS",

            // Oceania
            "FJ", "PG", "WS", "TO",
        };

    /// <summary>
    /// Mapping of country code → valid subdivision codes for that country.
    /// Only multi-timezone countries have state-level entries (since those are the only
    /// countries where state selection affects timezone resolution).
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<string>> ValidStateCodes { get; } =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["US"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NY", "FL", "GA", "NC", "VA", "MA", "PA", "NJ", "CT", "MD",
                "SC", "ME", "NH", "VT", "RI", "DE", "DC", "WV", "OH", "MI",
                "IL", "TX", "MN", "WI", "IA", "MO", "AR", "LA", "MS", "AL",
                "TN", "KY", "KS", "OK", "NE", "SD", "ND", "IN", "CO", "AZ",
                "UT", "MT", "WY", "NM", "ID", "CA", "WA", "OR", "NV", "AK", "HI"
            },
            ["RU"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "MOW", "SPE", "KDA", "ROS", "NIZ", "TAT", "SAM", "SVE", "CHE",
                "TYU", "OMS", "NVS", "KYA", "IRK", "PRI", "KHA", "SAK", "KAM"
            },
            ["AU"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NSW", "VIC", "QLD", "WA", "SA", "TAS", "NT", "ACT"
            },
            ["CA"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ON", "QC", "BC", "AB", "SK", "MB", "NS", "NB", "PE", "NL", "NT", "YT", "NU"
            },
            ["BR"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SP", "RJ", "MG", "PR", "RS", "SC", "BA", "AM", "PA", "MT", "MS", "AC", "CE", "PE"
            },
            ["MX"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CMX", "JAL", "NLE", "BCN", "BCS", "SIN", "SON", "CHH"
            },
            ["ID"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "JK", "JB", "JT", "JI", "KT", "KS", "BA", "NT", "PA", "PB"
            },
            ["CL"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "RM", "VS", "BI", "IP"
            },
            ["KZ"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ALA", "AST", "AKT", "MAN", "ATY"
            },
            ["CN"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "XJ"
            },
            ["MN"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "UB", "HOV"
            },
            ["CD"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "KN", "LB"
            },
            ["AR"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "BA", "CF", "SL"
            },
            ["PT"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "30", "20"
            },
            ["ES"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CN"
            },
            ["NZ"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CIT"
            },
        };

    /// <summary>
    /// Returns true if the given country code is valid (supported by the system).
    /// </summary>
    public static bool IsValidCountryCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        return ValidCountryCodes.Contains(code.Trim());
    }

    /// <summary>
    /// Returns true if the given state code is valid for the specified country.
    /// If the country has no state-level mappings (single-timezone), any state code is invalid
    /// since it would have no effect on timezone resolution.
    /// </summary>
    public static bool IsValidStateCode(string? countryCode, string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.IsNullOrWhiteSpace(stateCode))
            return false;

        var country = countryCode.Trim().ToUpperInvariant();
        if (!ValidStateCodes.TryGetValue(country, out var validStates))
            return false; // Country has no state-level mappings

        return validStates.Contains(stateCode.Trim());
    }

    /// <summary>
    /// Returns true if the given country has state-level subdivisions for timezone resolution.
    /// </summary>
    public static bool HasStateSubdivisions(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return false;
        return ValidStateCodes.ContainsKey(countryCode.Trim());
    }
}
