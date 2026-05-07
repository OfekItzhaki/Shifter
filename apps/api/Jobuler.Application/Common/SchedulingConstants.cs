namespace Jobuler.Application.Common;

/// <summary>
/// Named constants for scheduling configuration.
/// Centralises magic numbers so they're self-documenting and easy to change.
/// </summary>
public static class SchedulingConstants
{
    // ── Solver horizon ────────────────────────────────────────────────────────

    /// <summary>Default solver horizon when no group setting is configured (days).</summary>
    public const int DefaultHorizonDays = 7;

    /// <summary>Minimum allowed solver horizon (days).</summary>
    public const int MinHorizonDays = 1;

    /// <summary>Maximum allowed solver horizon via group settings (days).</summary>
    public const int MaxHorizonDays = 90;

    // ── Shift generation ──────────────────────────────────────────────────────

    /// <summary>
    /// Safety cap on shifts generated per task per solver run.
    /// Base value = 7 days × 48 half-hour slots. Scales with horizon.
    /// </summary>
    public const int BaseMaxShiftsPerTask = 336;

    /// <summary>Half-hour slots per day — used to scale the shift cap with horizon length.</summary>
    public const int SlotsPerDay = 48;

    // ── Objective weights ─────────────────────────────────────────────────────

    /// <summary>
    /// Penalty weight for each uncovered headcount slot.
    /// Very high so coverage is always prioritised over stability.
    /// </summary>
    public const int CoverageWeight = 1000;

    // ── Stability weights (sent in every solver payload) ──────────────────────

    /// <summary>Stability penalty weight for changes to today/tomorrow assignments.</summary>
    public const double StabilityWeightTodayTomorrow = 10.0;

    /// <summary>Stability penalty weight for changes to days 3–4 assignments.</summary>
    public const double StabilityWeightDays3To4 = 3.0;

    /// <summary>Stability penalty weight for changes to days 5–7 assignments.</summary>
    public const double StabilityWeightDays5To7 = 1.0;

    // ── Task horizon (frontend / API) ─────────────────────────────────────────

    /// <summary>
    /// Default task end horizon when creating a task with no explicit end date (days).
    /// Tasks created via import or quick-create default to 90 days ahead.
    /// </summary>
    public const int DefaultTaskHorizonDays = 90;

    // ── Solver timeout ────────────────────────────────────────────────────────

    /// <summary>Default solver timeout in seconds (overridden by SOLVER_TIMEOUT_SECONDS env var).</summary>
    public const int DefaultSolverTimeoutSeconds = 30;
}
