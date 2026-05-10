namespace Jobuler.Application.AI.Import;

/// <summary>
/// Defines the known column name mappings for structured CSV/Excel import.
/// Each canonical column key maps to an array of accepted variants (Hebrew and English).
/// Column matching is performed case-insensitively.
/// </summary>
public static class ImportColumnNames
{
    /// <summary>
    /// Required columns that must all be present for structured parsing to succeed.
    /// Keys are canonical column names; values are accepted header variants.
    /// </summary>
    public static readonly Dictionary<string, string[]> RequiredColumns = new()
    {
        ["person_name"] = new[] { "person_name", "שם", "name", "שם_מלא" },
        ["task_name"] = new[] { "task_name", "משימה", "task", "תפקיד" },
        ["day_of_week"] = new[] { "day_of_week", "יום", "day" },
        ["start_hour"] = new[] { "start_hour", "שעת_התחלה", "start", "התחלה" },
        ["end_hour"] = new[] { "end_hour", "שעת_סיום", "end", "סיום" },
    };

    /// <summary>
    /// Optional columns that enhance the parsed output when present.
    /// If absent, default values are used (shift_duration_hours=4, required_headcount=1).
    /// </summary>
    public static readonly Dictionary<string, string[]> OptionalColumns = new()
    {
        ["shift_duration_hours"] = new[] { "shift_duration_hours", "משך_משמרת", "duration" },
        ["required_headcount"] = new[] { "required_headcount", "נדרשים", "headcount" },
    };
}
