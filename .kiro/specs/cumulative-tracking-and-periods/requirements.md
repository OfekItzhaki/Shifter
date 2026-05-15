# Requirements Document

## Introduction

This feature adds cross-run memory to the Shifter scheduling system. Currently, the CP-SAT solver operates statelessly within a single planning horizon (1–7 days). This means home-leave eligibility only works when the horizon is at least as long as the eligibility threshold, fairness is only balanced within the current run, and historical assignment data is lost when new schedules are published.

The feature introduces three capabilities: (1) cumulative tracking that feeds historical days-at-base, assignment counts, and last-leave dates into the solver as input; (2) permanent historical statistics with daily snapshots and incremental counters; and (3) subscription periods that partition data into logical time segments tied to billing lifecycle.

## Glossary

- **Cumulative_Tracker**: The service responsible for maintaining and updating per-person cumulative counters across solver runs
- **Assignment_Snapshot_Service**: The service that records daily assignment snapshots after each schedule publish
- **Period_Manager**: The service responsible for creating, closing, and querying subscription periods
- **Solver_Payload_Builder**: The existing SolverPayloadNormalizer that constructs the solver input payload (to be extended with cumulative data)
- **Stats_API**: The API layer that serves historical and cumulative statistics to the frontend
- **Cumulative_Record**: A per-person row storing all-time and period-scoped counters (days at base, total assignments, last leave date, etc.)
- **Daily_Snapshot**: An immutable row recording one person's assignments and presence state for a single calendar day
- **Subscription_Period**: A time-bounded segment tied to a group's billing lifecycle (trial start → cancellation, or renewal → renewal)
- **Eligibility_Threshold**: The configured number of consecutive hours a person must be at base before becoming eligible for home-leave
- **Planning_Horizon**: The number of days (1–7) the solver schedules in a single run
- **Solver**: The Python CP-SAT scheduling engine that generates shift assignments

## Requirements

### Requirement 1: Cumulative Days-at-Base Tracking

**User Story:** As a group admin, I want the system to track how many consecutive hours each person has been at base across multiple solver runs, so that home-leave eligibility works correctly even when I generate schedules one day at a time.

#### Acceptance Criteria

1. WHEN a schedule version is published, THE Cumulative_Tracker SHALL update each person's consecutive_hours_at_base counter by computing hours spent in FreeInBase state since their last home-leave end or period start
2. WHEN a person's home-leave ends, THE Cumulative_Tracker SHALL reset that person's consecutive_hours_at_base counter to zero
3. WHEN the Solver_Payload_Builder constructs a solver payload, THE Solver_Payload_Builder SHALL include each person's current consecutive_hours_at_base value in the payload
4. WHEN the Solver receives a person's consecutive_hours_at_base value that meets or exceeds the Eligibility_Threshold, THE Solver SHALL treat that person as eligible for home-leave within the current Planning_Horizon regardless of the horizon length
5. THE Cumulative_Tracker SHALL store the last_home_leave_end timestamp per person per group
6. IF a schedule version is rolled back, THEN THE Cumulative_Tracker SHALL recompute consecutive_hours_at_base from the presence_windows table rather than decrementing the counter

### Requirement 2: Cumulative Assignment Counters

**User Story:** As a group admin, I want the system to track total assignment counts per person across all solver runs, so that fairness balancing considers the full history rather than just the current 7-day window.

#### Acceptance Criteria

1. WHEN a schedule version is published, THE Cumulative_Tracker SHALL increment each person's cumulative assignment counters (total_assignments, hard_tasks, disliked_hated_score, kitchen_count, night_missions) based on the assignments in that version
2. THE Solver_Payload_Builder SHALL include cumulative assignment counters alongside the existing 7-day rolling counters in the solver payload
3. WHEN the Solver computes fairness penalties, THE Solver SHALL use cumulative counters weighted by a configurable decay factor to balance long-term fairness against short-term fairness
4. THE Cumulative_Record SHALL maintain counters for the following time windows: 7-day, 14-day, 30-day, 90-day, and all-time-within-period
5. WHEN a new Subscription_Period starts, THE Cumulative_Tracker SHALL reset all-time-within-period counters to zero for all persons in the group

### Requirement 3: Daily Assignment Snapshots

**User Story:** As a group admin, I want the system to keep a permanent record of who was assigned where on each day, so that I can view historical statistics for any time range.

#### Acceptance Criteria

1. WHEN a schedule version is published, THE Assignment_Snapshot_Service SHALL create one Daily_Snapshot row per person per calendar day covered by that version
2. THE Daily_Snapshot SHALL record: person_id, date, group_id, space_id, task_type_id, slot_id, shift_start, shift_end, burden_level, and period_id
3. IF a new schedule version is published that overlaps with existing snapshots, THEN THE Assignment_Snapshot_Service SHALL replace the overlapping snapshots with data from the new version
4. THE Assignment_Snapshot_Service SHALL preserve snapshots from dates not covered by the new version unchanged
5. THE Daily_Snapshot rows SHALL be immutable after the calendar day has passed (only future-dated snapshots may be replaced)

### Requirement 4: Incremental Aggregated Statistics

**User Story:** As a group admin, I want to view assignment statistics for any time range without waiting for expensive recomputations, so that the stats page loads quickly.

#### Acceptance Criteria

1. WHEN a Daily_Snapshot is created or replaced, THE Cumulative_Tracker SHALL update the corresponding aggregated counters incrementally (add new values, subtract replaced values)
2. THE Stats_API SHALL support querying statistics for the following time ranges: 7 days, 14 days, 30 days, 90 days, and all-time-within-period
3. WHEN the Stats_API receives a time-range query, THE Stats_API SHALL return results within 200ms for groups with up to 50 members and 90 days of history
4. THE Stats_API SHALL return per-person statistics including: total_assignments, hard_tasks, kitchen_count, night_missions, disliked_hated_score, total_hours_assigned, and average_daily_burden
5. THE Stats_API SHALL support filtering statistics by group_id and by period_id

### Requirement 5: Subscription Period Lifecycle

**User Story:** As a system operator, I want data to be partitioned into subscription periods, so that statistics reset when a group starts fresh after a billing lapse and historical data from previous periods remains accessible separately.

#### Acceptance Criteria

1. WHEN a GroupSubscription transitions to Active or Trialing status, THE Period_Manager SHALL create a new Subscription_Period record with starts_at set to the current timestamp
2. WHEN a GroupSubscription transitions to Canceled status and the grace period (14 days) elapses without reactivation, THE Period_Manager SHALL close the current Subscription_Period by setting ends_at to the cancellation timestamp
3. WHEN a new Subscription_Period starts, THE Cumulative_Tracker SHALL reset all cumulative counters for that group to zero
4. THE Period_Manager SHALL preserve all Daily_Snapshots and Cumulative_Records from closed periods with their original period_id intact
5. WHEN the Stats_API receives a query without an explicit period_id filter, THE Stats_API SHALL return statistics scoped to the current active Subscription_Period
6. THE Stats_API SHALL support querying statistics from previous closed periods by specifying a period_id parameter

### Requirement 6: Solver Payload Extension for Cumulative Data

**User Story:** As a solver developer, I want the solver payload to include cumulative tracking data, so that the solver can make informed decisions about home-leave eligibility and fairness without needing to query historical data itself.

#### Acceptance Criteria

1. THE Solver_Payload_Builder SHALL include a cumulative_tracking section in the solver payload containing: consecutive_hours_at_base, last_home_leave_end, total_assignments_in_period, hard_tasks_in_period, and days_since_last_leave per person
2. WHEN the Solver evaluates home-leave eligibility, THE Solver SHALL add consecutive_hours_at_base from the cumulative_tracking payload to the hours computed within the current horizon before comparing against the Eligibility_Threshold
3. WHEN the Solver computes fairness objectives, THE Solver SHALL incorporate total_assignments_in_period from the cumulative_tracking payload as a bias term in the fairness penalty function
4. IF cumulative_tracking data is missing for a person (new member), THEN THE Solver SHALL treat that person as having zero cumulative history and compute eligibility solely from the current horizon

### Requirement 7: Historical Statistics API

**User Story:** As a frontend developer, I want API endpoints that serve cumulative and historical statistics with flexible time-range filtering, so that I can build stats dashboards showing trends over weeks and months.

#### Acceptance Criteria

1. THE Stats_API SHALL expose a GET endpoint that returns per-person cumulative statistics for a specified time range and optional group and period filters
2. THE Stats_API SHALL expose a GET endpoint that returns daily time-series data (assignments per day, burden per day) for charting
3. WHEN a time-range query spans multiple Subscription_Periods, THE Stats_API SHALL return data only from the current period unless the caller explicitly requests cross-period data
4. THE Stats_API SHALL include the period metadata (period_id, starts_at, ends_at, status) in responses that contain period-scoped data
5. THE Stats_API SHALL enforce space-level tenant isolation on all statistics queries by requiring space_id in the request path and filtering all data by space_id

### Requirement 8: Presence-Based Eligibility Recomputation

**User Story:** As a group admin, I want the system to correctly recompute days-at-base when presence windows are manually edited, so that eligibility remains accurate after admin corrections.

#### Acceptance Criteria

1. WHEN an admin creates, updates, or deletes a PresenceWindow with state AtHome, THE Cumulative_Tracker SHALL recompute consecutive_hours_at_base for the affected person from the presence_windows table
2. WHEN an admin cancels an in-progress home-leave (truncates a PresenceWindow), THE Cumulative_Tracker SHALL begin counting consecutive_hours_at_base from the truncation timestamp
3. THE Cumulative_Tracker SHALL compute consecutive_hours_at_base by summing all contiguous FreeInBase time since the most recent AtHome window end or period start, whichever is later
4. IF no AtHome window exists for a person within the current period, THEN THE Cumulative_Tracker SHALL compute consecutive_hours_at_base from the period start date


### Requirement 9: Historical Schedule Viewing

**User Story:** As a group member, I want to view the schedule for any past day (yesterday, last week, etc.), so that I can check what shifts were assigned and verify my history.

#### Acceptance Criteria

1. THE Schedule_API SHALL expose a GET endpoint that returns assignments for a specific group and date range, sourced from Daily_Snapshots
2. WHEN a user navigates to a past week in the schedule tab, THE frontend SHALL fetch and display the historical assignments for that week from the Daily_Snapshots
3. THE group admin SHALL be able to configure a schedule_history_retention_days setting (default: unlimited) that limits how far back members can view past schedules
4. IF schedule_history_retention_days is set and a user requests a date older than the retention limit, THE Schedule_API SHALL return an empty result with a message indicating the data is outside the retention window
5. THE Schedule_API SHALL return historical assignments in the same format as the current schedule endpoint, so the frontend can render them identically without special handling
6. WHEN viewing historical schedules, THE frontend SHALL clearly indicate that the user is viewing a past date (not the current schedule)
