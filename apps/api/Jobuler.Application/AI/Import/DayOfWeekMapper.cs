namespace Jobuler.Application.AI.Import;

/// <summary>
/// Normalizes day-of-week values from various Hebrew and English representations
/// to a canonical lowercase English day name.
/// Supports: full English, abbreviated English, Hebrew abbreviations (with/without geresh),
/// and full Hebrew day names.
/// </summary>
public static class DayOfWeekMapper
{
    private static readonly Dictionary<string, string> Mappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // English full names
        ["sunday"] = "sunday",
        ["monday"] = "monday",
        ["tuesday"] = "tuesday",
        ["wednesday"] = "wednesday",
        ["thursday"] = "thursday",
        ["friday"] = "friday",
        ["saturday"] = "saturday",

        // English abbreviated names
        ["sun"] = "sunday",
        ["mon"] = "monday",
        ["tue"] = "tuesday",
        ["wed"] = "wednesday",
        ["thu"] = "thursday",
        ["fri"] = "friday",
        ["sat"] = "saturday",

        // Hebrew abbreviations with geresh (׳)
        ["א׳"] = "sunday",
        ["ב׳"] = "monday",
        ["ג׳"] = "tuesday",
        ["ד׳"] = "wednesday",
        ["ה׳"] = "thursday",
        ["ו׳"] = "friday",
        ["ש׳"] = "saturday",

        // Hebrew abbreviations without geresh
        ["א"] = "sunday",
        ["ב"] = "monday",
        ["ג"] = "tuesday",
        ["ד"] = "wednesday",
        ["ה"] = "thursday",
        ["ו"] = "friday",
        ["ש"] = "saturday",

        // Hebrew full day names
        ["ראשון"] = "sunday",
        ["שני"] = "monday",
        ["שלישי"] = "tuesday",
        ["רביעי"] = "wednesday",
        ["חמישי"] = "thursday",
        ["שישי"] = "friday",
        ["שבת"] = "saturday",
    };

    /// <summary>
    /// Normalizes a day-of-week input string to a lowercase English day name.
    /// Returns null if the input is not a recognized day representation.
    /// </summary>
    public static string? Normalize(string input) =>
        Mappings.TryGetValue(input.Trim(), out var day) ? day : null;
}
