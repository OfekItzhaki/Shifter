# Requirements Document

## Introduction

This feature replaces the existing home-leave configuration form with an interactive **slider control** that lets admins intuitively balance between "let more people go home" and "keep more people at base." Instead of manually configuring numeric fields (`leave_capacity`, `priority_weight`), the admin moves a single slider and sees a real-time preview of the schedule impact — how many people will be home vs. at base, coverage gaps, and fairness metrics.

The slider maps to the solver's `priority_weight` parameter (controlling how aggressively the solver maximizes home-leave assignments). A lightweight preview solver run provides near-instant feedback so admins can make informed decisions before committing to a full solver run.

---

## Glossary

- **System**: The Jobuler backend (ASP.NET Core API + Application + Domain + Infrastructure layers).
- **Solver**: The Python CP-SAT scheduling engine (`apps/solver`) using Google OR-Tools.
- **UI**: The Jobuler Next.js frontend (`apps/web`).
- **Slider**: A horizontal range input control with two semantic endpoints: "יותר אנשים בבית" (more people home) on one side and "יותר אנשים בבסיס" (more people at base) on the other.
- **Balance_Value**: An integer between 0 and 100 representing the slider position. 0 = maximize base coverage (minimize home-leave), 100 = maximize home-leave (let as many people go home as possible).
- **Preview_Run**: A lightweight, time-bounded solver execution that produces an estimated schedule impact without creating a full draft version.
- **Preview_Result**: The output of a preview run containing estimated counts of people at home vs. at base per time slot, coverage metrics, and fairness indicators.
- **Home_Leave_Config**: The existing `home_leave_configs` table record for a closed-base group, extended with a `balance_value` field.
- **Coverage_Gap**: A time slot where the number of people available at base drops below the minimum required for operational coverage.
- **Impact_Summary**: A UI panel showing the concrete effect of the current slider position: people home, people at base, coverage gaps, and fairness spread.
- **Debounce_Interval**: The minimum time (in milliseconds) between consecutive preview requests triggered by slider movement.
- **Preview_Timeout**: The maximum time (in seconds) the preview solver is allowed to run before returning a best-effort result.
- **Group_Admin**: A user with the `constraints.manage` permission scoped to the group's space.

---

## Requirements

### Requirement 1: Slider UI Component

**User Story:** As a group admin of a closed-base group, I want a slider control that visually represents the balance between home-leave and base coverage, so that I can intuitively adjust the scheduling priority without understanding numeric parameters.

#### Acceptance Criteria

1. WHILE `is_closed_base` is `true` for the group, THE UI SHALL display a horizontal slider component in the home-leave configuration section, replacing the existing `priority_weight` numeric input field.
2. THE UI SHALL label the left end of the slider "יותר אנשים בבסיס" (more people at base) and the right end "יותר אנשים בבית" (more people home).
3. THE Slider SHALL accept integer values between 0 and 100 inclusive, with 0 representing maximum base coverage priority and 100 representing maximum home-leave priority.
4. WHEN the admin opens the home-leave configuration panel for a group with an existing `balance_value`, THE UI SHALL position the slider at the stored value.
5. WHEN the admin opens the home-leave configuration panel for a group with no stored `balance_value`, THE UI SHALL position the slider at the default value of 50 (balanced).
6. THE UI SHALL display the current numeric value (0–100) adjacent to the slider track as the admin moves the slider.
7. THE Slider SHALL be keyboard-accessible: arrow keys SHALL increment or decrement the value by 1, and Page Up/Page Down SHALL increment or decrement by 10.

---

### Requirement 2: Balance Value Persistence

**User Story:** As a group admin, I want the slider position to be saved as part of the home-leave configuration, so that the solver uses my chosen balance when generating schedules.

#### Acceptance Criteria

1. THE System SHALL add a `balance_value` integer column (NOT NULL, default 50) to the `home_leave_configs` table.
2. WHEN the admin saves the home-leave configuration, THE UI SHALL include the current slider `balance_value` in the `PUT /spaces/{spaceId}/groups/{groupId}/home-leave-config` request body.
3. THE System SHALL validate that `balance_value` is an integer between 0 and 100 inclusive; IF the value is outside this range, THEN THE System SHALL return HTTP 400 with an error message indicating the allowed range.
4. THE System SHALL persist the `balance_value` alongside the other home-leave configuration fields in the `home_leave_configs` record.
5. THE System SHALL include `balance_value` in the response of `GET /spaces/{spaceId}/groups/{groupId}/home-leave-config`.
6. THE System SHALL require the `constraints.manage` permission for updating the `balance_value`; IF the caller lacks this permission, THEN THE System SHALL return HTTP 403.

---

### Requirement 3: Solver Integration with Balance Value

**User Story:** As a developer, I want the solver to use the balance value to control how aggressively it schedules home-leave, so that the slider position directly affects the generated schedule.

#### Acceptance Criteria

1. THE System SHALL include `balance_value` (integer, 0–100) in the `home_leave_config` field of the solver payload when building the input for a closed-base group.
2. WHEN `balance_value` is 0, THE Solver SHALL set the home-leave eligibility preference weight to 0, effectively disabling the soft preference to send people home (hard constraints like capacity still apply if people are manually assigned).
3. WHEN `balance_value` is 100, THE Solver SHALL set the home-leave eligibility preference weight to its maximum configured value (400), maximizing the solver's preference to assign home-leave slots.
4. FOR ALL `balance_value` values between 1 and 99, THE Solver SHALL linearly interpolate the home-leave eligibility preference weight between 0 and 400 (weight = balance_value × 4).
5. THE Solver SHALL NOT modify hard constraints (capacity, min rest, no-overlap) based on the `balance_value`; only soft preference weights SHALL be affected.
6. WHEN `balance_value` is absent from the solver payload (backward compatibility), THE Solver SHALL use the default weight of 200 (equivalent to balance_value = 50).

---

### Requirement 4: Preview Solver Endpoint

**User Story:** As a group admin, I want to see a quick preview of the schedule impact when I move the slider, so that I can make an informed decision before committing to a full solver run.

#### Acceptance Criteria

1. THE System SHALL expose a new endpoint `POST /spaces/{spaceId}/groups/{groupId}/home-leave-preview` that accepts a request body containing `balance_value` (integer, 0–100).
2. WHEN the preview endpoint is called, THE System SHALL build a solver payload identical to a normal solver run for the group, but with the provided `balance_value` overriding the stored value.
3. THE System SHALL dispatch the preview solver run as a synchronous HTTP call to the solver service with a timeout of 5 seconds.
4. THE Solver SHALL support a `preview_mode` flag in the input payload; WHEN `preview_mode` is `true`, THE Solver SHALL use a reduced time limit (3 seconds) and return the best solution found within that limit, even if not optimal.
5. IF the preview solver run times out without finding any feasible solution, THEN THE System SHALL return HTTP 200 with a response indicating `status: "no_solution"` and an empty impact summary.
6. IF the preview solver run completes (with or without optimality), THEN THE System SHALL return HTTP 200 with the preview result containing the impact summary.
7. THE System SHALL require the `constraints.manage` permission for the preview endpoint; IF the caller lacks this permission, THEN THE System SHALL return HTTP 403.
8. THE System SHALL NOT persist the preview result as a draft schedule version; preview results are ephemeral and returned only in the HTTP response.
9. IF the group does not exist, is not closed-base, or has no home-leave configuration, THEN THE System SHALL return HTTP 400 with an appropriate error message.

---

### Requirement 5: Preview Result Format

**User Story:** As a group admin, I want the preview to show me concrete numbers about who will be home and who will be at base, so that I can understand the real impact of my slider choice.

#### Acceptance Criteria

1. THE System SHALL return a preview result containing: `people_home_count` (integer — number of distinct people assigned at least one home-leave slot), `people_at_base_count` (integer — number of people with no home-leave assignment), `total_home_leave_slots` (integer — total number of home-leave assignments across all people), `coverage_gaps` (array of objects with `starts_at`, `ends_at`, and `available_count` for time windows where available people drop below a threshold), and `fairness_spread` (decimal — difference between highest and lowest `base_time_ratio`).
2. THE System SHALL calculate `coverage_gaps` as time windows where the number of people available at base (not on leave and not on mission) drops below `group_member_count - leave_capacity`.
3. WHEN the preview result contains zero coverage gaps, THE Impact_Summary SHALL display a "כיסוי מלא" (full coverage) indicator.
4. WHEN the preview result contains one or more coverage gaps, THE Impact_Summary SHALL display a warning with the total number of gap hours and the minimum available count during those gaps.
5. THE System SHALL include a `status` field in the preview response with possible values: `"optimal"` (solver found proven optimal), `"feasible"` (solver found a solution but not proven optimal within time limit), and `"no_solution"` (no feasible solution found within time limit).
6. THE System SHALL include a `solver_time_ms` field (integer) indicating how many milliseconds the preview solver took to produce the result.

---

### Requirement 6: Frontend Preview Integration

**User Story:** As a group admin, I want the preview to update automatically as I move the slider, so that I get immediate visual feedback without clicking a separate button.

#### Acceptance Criteria

1. WHEN the admin moves the slider and releases it (on `change` event), THE UI SHALL send a preview request to `POST /spaces/{spaceId}/groups/{groupId}/home-leave-preview` with the current `balance_value`.
2. THE UI SHALL debounce preview requests with a 500ms interval; IF the slider value changes again within 500ms of the last change, THE UI SHALL cancel the pending request and schedule a new one with the latest value.
3. WHILE a preview request is in flight, THE UI SHALL display a loading indicator within the impact summary panel.
4. WHEN a preview response is received, THE UI SHALL update the impact summary panel with the new data, replacing any previous preview result.
5. IF a preview request fails (network error or HTTP 5xx), THE UI SHALL display an error message "לא ניתן לטעון תצוגה מקדימה" (cannot load preview) within the impact summary panel and retain the last successful preview result if available.
6. WHEN the admin first opens the home-leave configuration panel for a group with an existing `balance_value`, THE UI SHALL automatically trigger a preview request with the stored value to populate the impact summary.
7. IF a newer preview request is dispatched while an older one is still in flight, THE UI SHALL ignore the response from the older request when it arrives.

---

### Requirement 7: Impact Summary Display

**User Story:** As a group admin, I want to see a clear summary of the schedule impact, so that I can quickly understand the tradeoff between home-leave and base coverage.

#### Acceptance Criteria

1. THE UI SHALL display the impact summary panel directly below the slider component within the home-leave configuration section.
2. THE Impact_Summary SHALL display the following metrics in Hebrew: "אנשים בבית" (people home) with the `people_home_count` value, "אנשים בבסיס" (people at base) with the `people_at_base_count` value, "סה״כ חופשות" (total leaves) with the `total_home_leave_slots` value, and "פער הוגנות" (fairness spread) with the `fairness_spread` value as a percentage.
3. THE Impact_Summary SHALL display a horizontal bar visualization showing the ratio of people home vs. people at base, using distinct colors for each segment.
4. WHEN `fairness_spread` exceeds 0.15 (15 percentage points), THE Impact_Summary SHALL highlight the fairness metric with a warning color and display a tooltip explaining that the distribution is uneven.
5. WHEN the preview `status` is `"no_solution"`, THE Impact_Summary SHALL display a message "לא נמצא פתרון עם ההגדרות הנוכחיות" (no solution found with current settings) instead of metrics.
6. WHEN the preview `status` is `"feasible"` (not optimal), THE Impact_Summary SHALL display a subtle indicator "תוצאה משוערת" (estimated result) to communicate that the numbers may improve with a full solver run.

---

### Requirement 8: Preview Solver Mode in Python Solver

**User Story:** As a developer, I want the solver to support a fast preview mode, so that it can return approximate results within the time budget required for interactive feedback.

#### Acceptance Criteria

1. THE Solver SHALL accept a `preview_mode` boolean field in the `SolverInput` model (default: `false`).
2. WHEN `preview_mode` is `true`, THE Solver SHALL set the CP-SAT solver time limit to 3 seconds.
3. WHEN `preview_mode` is `true`, THE Solver SHALL use a simplified search strategy: set `num_workers` to 1 and disable solution logging to reduce overhead.
4. WHEN `preview_mode` is `true` and the solver finds at least one feasible solution within the time limit, THE Solver SHALL return that solution with `status = "feasible"` or `status = "optimal"` (if proven optimal).
5. WHEN `preview_mode` is `true` and the solver finds no feasible solution within the time limit, THE Solver SHALL return `status = "no_solution"` with empty assignment lists.
6. THE Solver SHALL include `solver_time_ms` (integer) in the output indicating the actual wall-clock time spent solving.
7. WHEN `preview_mode` is `false` or absent, THE Solver SHALL use the standard time limit and worker configuration (no behavioral change from current implementation).

---

### Requirement 9: Database Schema Extension

**User Story:** As a developer, I want the database schema to support the balance value, so that the slider position is persisted and available for solver runs.

#### Acceptance Criteria

1. THE System SHALL add a `balance_value` integer column to the `home_leave_configs` table with a NOT NULL constraint and a default value of 50.
2. THE System SHALL create a database migration that adds the column with a default value, ensuring existing records receive the default without requiring manual data backfill.
3. THE System SHALL add a CHECK constraint ensuring `balance_value` is between 0 and 100 inclusive.
4. THE System SHALL update the `HomeLeaveConfig` domain entity to include a `BalanceValue` property (int, default 50).
5. THE System SHALL update the `HomeLeaveConfig.Create` and `HomeLeaveConfig.Update` methods to accept and validate the `balance_value` parameter.

---

### Requirement 10: Backward Compatibility

**User Story:** As a developer, I want the slider feature to be backward-compatible with existing configurations, so that groups with existing home-leave settings continue to work without manual intervention.

#### Acceptance Criteria

1. WHEN the `balance_value` column is added to existing `home_leave_configs` records, THE System SHALL set the default value to 50 for all existing records.
2. WHEN the solver receives a payload without a `balance_value` field in `home_leave_config`, THE Solver SHALL use a default value of 50 (equivalent to the current behavior with weight 200).
3. THE System SHALL continue to accept the existing `PUT /spaces/{spaceId}/groups/{groupId}/home-leave-config` request format without `balance_value`; IF `balance_value` is omitted from the request body, THE System SHALL retain the currently stored value (or use 50 if no value exists).
4. THE UI SHALL continue to display and allow editing of the existing configuration fields (`min_rest_hours`, `eligibility_threshold_hours`, `leave_capacity`, `leave_duration_hours`) alongside the new slider; the slider does not replace these fields, only the `priority_weight` concept.

