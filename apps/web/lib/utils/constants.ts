/**
 * Named constants for the frontend application.
 * Centralises magic numbers so they're self-documenting and easy to change.
 */

// ── Task horizon ────────────────────────────────────────────────────────────

/**
 * Default task end horizon when creating a task with no explicit end date (days).
 * Tasks created via import or quick-create default to 90 days ahead.
 */
export const DEFAULT_TASK_HORIZON_DAYS = 90;

/**
 * Milliseconds in a day — used for date arithmetic.
 */
export const MS_PER_DAY = 86400000; // 24 * 60 * 60 * 1000

// ── Solver settings ──────────────────────────────────────────────────────────

/**
 * Minimum allowed solver horizon (days).
 */
export const MIN_SOLVER_HORIZON_DAYS = 1;

/**
 * Maximum allowed solver horizon (days).
 */
export const MAX_SOLVER_HORIZON_DAYS = 90;

/**
 * Default solver horizon (days).
 */
export const DEFAULT_SOLVER_HORIZON_DAYS = 7;
