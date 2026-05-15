# Implementation Plan: Cumulative Tracking and Periods

## Overview

Add cross-run memory to the scheduling system via three subsystems: cumulative tracking (per-person counters persisted across solver runs), daily snapshots (immutable per-person-per-day assignment records), and subscription periods (billing-lifecycle partitions). Implementation follows a 5-phase migration strategy: schema → backfill → application layer → solver extension → API + frontend.

## Tasks

- [x] 1. Schema Migration — new tables and columns
  - [x] 1.1 Create `subscription_periods` table
    - Create migration file `052_cumulative_tracking.sql`
    - Define table with columns: id, space_id, group_id, status (active|closed), starts_at, ends_at, created_at
    - Add indexes: `idx_subscription_periods_group`, `idx_subscription_periods_active` (partial WHERE status='active')
    - Enable RLS with `subscription_periods_isolation` policy filtering by `app.current_space_id`
    - _Requirements: 5.1, 5.2, 5.4_
  - [x] 1.2 Create `cumulative_records` table
    - Define table with columns: id, space_id, group_id, person_id, period_id, consecutive_hours_at_base, last_home_leave_end, multi-window counters (7d/14d/30d/90d/period for total_assignments, hard_tasks, disliked_hated_score, kitchen_count, night_missions), total_hours_assigned_period, updated_at
    - Add UNIQUE constraint on (space_id, group_id, person_id, period_id)
    - Add indexes: `idx_cumulative_records_lookup`, `idx_cumulative_records_person`
    - Enable RLS with `cumulative_records_isolation` policy
    - _Requirements: 1.1, 1.5, 2.1, 2.4_
  - [x] 1.3 Create `daily_snapshots` table
    - Define table with columns: id, space_id, group_id, person_id, period_id, snapshot_date, task_type_id, slot_id, shift_start, shift_end, burden_level, version_id, created_at
    - Add UNIQUE constraint on (space_id, group_id, person_id, snapshot_date, slot_id)
    - Add indexes: `idx_daily_snapshots_date_range`, `idx_daily_snapshots_person`, `idx_daily_snapshots_period`
    - Enable RLS with `daily_snapshots_isolation` policy
    - _Requirements: 3.1, 3.2_
  - [x] 1.4 Add `schedule_history_retention_days` column to `groups` table
    - ALTER TABLE groups ADD COLUMN schedule_history_retention_days INT DEFAULT NULL (NULL = unlimited)
    - _Requirements: 9.3_

- [x] 2. Domain Entities and Value Objects
  - [x] 2.1 Create `SubscriptionPeriod` domain entity
    - Implement in `Jobuler.Domain` with properties: SpaceId, GroupId, Status, StartsAt, EndsAt
    - Implement `ITenantScoped` interface
    - Add `Create(spaceId, groupId)` factory method setting status="active" and StartsAt=UtcNow
    - Add `Close()` method that sets status="closed" and EndsAt=UtcNow, throws if not active
    - Add `IsActive` computed property
    - _Requirements: 5.1, 5.2_
  - [x] 2.2 Create `CumulativeRecord` domain entity
    - Implement in `Jobuler.Domain` with all counter properties (multi-window)
    - Implement `ITenantScoped` interface
    - Add `ResetPeriodCounters()` method zeroing all *_period fields and consecutive_hours_at_base
    - Add `UpdateConsecutiveHours(decimal hours, DateTime? lastLeaveEnd)` method
    - Add `IncrementCounters(AssignmentCountsDelta delta)` method
    - _Requirements: 1.1, 1.2, 1.5, 2.1, 2.4, 2.5_
  - [x] 2.3 Create `DailySnapshot` domain entity
    - Implement in `Jobuler.Domain` with properties: SpaceId, GroupId, PersonId, PeriodId, SnapshotDate, TaskTypeId, SlotId, ShiftStart, ShiftEnd, BurdenLevel, VersionId
    - Implement `ITenantScoped` interface
    - Add `IsPast` computed property (SnapshotDate < today UTC)
    - _Requirements: 3.1, 3.2, 3.5_
  - [x] 2.4 Create `AssignmentCountsDelta` and `SnapshotDiff` value objects
    - Define `AssignmentCountsDelta` record: TotalAssignments, HardTasks, DislikedHatedScore, KitchenCount, NightMissions, TotalHours
    - Define `SnapshotDiff` record: Added, Replaced, Preserved, ReplacedDeltas
    - _Requirements: 2.1, 4.1_

- [x] 3. Checkpoint — Schema and domain verification
  - Ensure migration applies cleanly and domain entities compile without errors. Ask the user if questions arise.

- [x] 4. EF Core Configuration and Repository Setup
  - [x] 4.1 Add EF Core entity configurations for new entities
    - Create `SubscriptionPeriodConfiguration`, `CumulativeRecordConfiguration`, `DailySnapshotConfiguration` in Infrastructure
    - Map all columns, configure UNIQUE constraints, and set up FK relationships
    - Register entities in `AppDbContext`
    - _Requirements: 5.1, 1.1, 3.1_
  - [x] 4.2 Create `CumulativeTrackingDto` for solver payload
    - Define record in Application layer: PersonId, ConsecutiveHoursAtBase, LastHomeLeaveEnd, TotalAssignmentsInPeriod, HardTasksInPeriod, DaysSinceLastLeave
    - Add JSON property name attributes matching Python model expectations
    - _Requirements: 6.1_

- [x] 5. Backfill Script — populate from existing data
  - [x] 5.1 Create backfill command to generate initial subscription periods
    - For each group with an active subscription, create a `SubscriptionPeriod` with starts_at = subscription created_at
    - Handle groups without subscriptions gracefully (skip with log)
    - _Requirements: 5.1_
  - [x] 5.2 Create backfill command to generate daily snapshots from existing data
    - For each published schedule_version, join with assignments and task_slots to generate snapshot rows
    - Only create snapshots for the most recent published version covering each date (skip rolled-back versions)
    - Use ON CONFLICT to handle duplicates
    - _Requirements: 3.1, 3.2_
  - [x] 5.3 Create backfill command to compute initial cumulative records
    - For each person in each group, compute assignment counters from backfilled snapshots
    - Compute consecutive_hours_at_base from presence_windows table
    - Compute last_home_leave_end from most recent AtHome presence window
    - _Requirements: 1.1, 1.5, 2.1, 2.4_
  - [ ]* 5.4 Write verification query comparing backfilled 7d counters against existing fairness_counters
    - Compare cumulative_records total_assignments_7d with existing fairness data
    - Log discrepancies for manual review
    - _Requirements: 2.4_

- [x] 6. Checkpoint — Backfill verification
  - Ensure backfill commands run without errors and produce correct data. Ask the user if questions arise.

- [x] 7. Application Layer — AssignmentSnapshotService
  - [x] 7.1 Implement `IAssignmentSnapshotService` interface and service class
    - Define interface with `CreateSnapshotsAsync` and `GetHistoricalAsync` methods
    - Implement `CreateSnapshotsAsync`: extract assignments from published version, generate one DailySnapshot per person per day per slot
    - Replace future-dated overlapping snapshots (ON CONFLICT UPDATE for snapshot_date >= today)
    - Preserve past-dated snapshots (skip if snapshot_date < today)
    - Return SnapshotDiff with added/replaced/preserved counts and replaced deltas
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_
  - [x] 7.2 Implement `GetHistoricalAsync` for schedule history viewing
    - Query daily_snapshots by space_id, group_id, and date range
    - Check group's schedule_history_retention_days setting
    - Return empty result with retention_exceeded flag if date is outside retention window
    - _Requirements: 9.1, 9.3, 9.4, 9.5_
  - [ ]* 7.3 Write property test: Snapshot creation completeness (FsCheck)
    - **Property 7: Snapshot Creation Completeness**
    - For any published version with P persons and D days and S slots, verify exactly P×D×S snapshot rows are created with all required fields populated
    - **Validates: Requirements 3.1, 3.2**
  - [ ]* 7.4 Write property test: Snapshot replacement preserves non-overlapping data (FsCheck)
    - **Property 8: Snapshot Replacement Preserves Non-Overlapping Data**
    - For two versions V1 and V2 with overlapping date ranges, verify V2 replaces only overlapping future dates and V1 data for non-overlapping dates remains unchanged
    - **Validates: Requirements 3.3, 3.4**
  - [ ]* 7.5 Write property test: Past-dated snapshot immutability (FsCheck)
    - **Property 9: Past-Dated Snapshot Immutability**
    - For any snapshot with snapshot_date < today, verify no publish operation modifies it
    - **Validates: Requirements 3.5**

- [x] 8. Application Layer — CumulativeTracker
  - [x] 8.1 Implement `ICumulativeTracker` interface and service class
    - Define interface with `UpdateOnPublishAsync`, `RecomputeForPersonAsync`, `ResetPeriodCountersAsync`, `GetForSolverPayloadAsync`
    - _Requirements: 1.1, 1.2, 1.5, 1.6, 2.1, 2.5_
  - [x] 8.2 Implement `UpdateOnPublishAsync` — increment counters on publish
    - Accept version ID, load assignments from that version
    - Compute AssignmentCountsDelta per person (categorize by burden_level)
    - Increment all time-window counters on the person's CumulativeRecord
    - Recompute consecutive_hours_at_base from presence_windows (sum contiguous FreeInBase since last AtHome end or period start)
    - Update last_home_leave_end if applicable
    - _Requirements: 1.1, 1.5, 2.1_
  - [x] 8.3 Implement `RecomputeForPersonAsync` — full recomputation from presence_windows
    - Query all presence_windows for the person within the current period
    - Find most recent AtHome window end (or period start if none)
    - Sum contiguous FreeInBase hours from that point forward
    - Update CumulativeRecord with recomputed value
    - _Requirements: 1.6, 8.1, 8.2, 8.3, 8.4_
  - [x] 8.4 Implement `ResetPeriodCountersAsync` — reset on new period
    - Load all CumulativeRecords for the group
    - Call ResetPeriodCounters() on each, update period_id to new period
    - _Requirements: 2.5, 5.3_
  - [x] 8.5 Implement `GetForSolverPayloadAsync` — return data for solver
    - Query CumulativeRecords for the group's current period
    - Map to CumulativeTrackingDto list
    - For persons without a record (new members), return zero-valued DTO
    - _Requirements: 1.3, 2.2, 6.1, 6.4_
  - [ ]* 8.6 Write property test: Consecutive hours computation correctness (FsCheck)
    - **Property 1: Consecutive Hours Computation Correctness**
    - For any sequence of presence windows (FreeInBase, AtHome, OnMission), verify consecutive_hours_at_base equals total contiguous FreeInBase hours since most recent AtHome end or period start
    - **Validates: Requirements 1.1, 1.6, 8.1, 8.3, 8.4**
  - [ ]* 8.7 Write property test: Home-leave end resets counter (FsCheck)
    - **Property 2: Home-Leave End Resets Counter**
    - For any person whose AtHome window ends, verify consecutive_hours_at_base is zero immediately after, and subsequent FreeInBase time accumulates from that point
    - **Validates: Requirements 1.2, 8.2**
  - [ ]* 8.8 Write property test: Cumulative counter increment correctness (FsCheck)
    - **Property 5: Cumulative Counter Increment Correctness**
    - For any published version with a set of assignments, verify each person's counters are incremented by exactly their assignment count categorized by burden level
    - **Validates: Requirements 2.1**
  - [ ]* 8.9 Write property test: Period start resets all-time counters (FsCheck)
    - **Property 6: Period Start Resets All-Time Counters**
    - For any group transitioning to a new period, verify all *_period counters and consecutive_hours_at_base are zero after reset regardless of previous values
    - **Validates: Requirements 2.5, 5.3**
  - [ ]* 8.10 Write property test: Incremental aggregation equals full recomputation (FsCheck)
    - **Property 10: Incremental Aggregation Equals Full Recomputation**
    - For any sequence of snapshot create/replace operations, verify incrementally computed counters equal a full aggregation over all current snapshots
    - **Validates: Requirements 4.1**

- [x] 9. Application Layer — PeriodManager
  - [x] 9.1 Implement `IPeriodManager` interface and service class
    - Define interface with `OpenPeriodAsync`, `ClosePeriodAsync`, `GetCurrentPeriodAsync`
    - Implement `OpenPeriodAsync`: create new SubscriptionPeriod, call CumulativeTracker.ResetPeriodCountersAsync
    - Implement `ClosePeriodAsync`: find active period, call Close(), preserve all associated data
    - Implement `GetCurrentPeriodAsync`: query for active period by group
    - _Requirements: 5.1, 5.2, 5.3, 5.4_
  - [ ]* 9.2 Write property test: Period lifecycle preserves historical data (FsCheck)
    - **Property 12: Period Lifecycle Preserves Historical Data**
    - For any closed period, verify all daily_snapshots and cumulative_records remain queryable with original period_id and are not modified by subsequent period operations
    - **Validates: Requirements 5.4**

- [x] 10. Checkpoint — Application services verification
  - Ensure all application layer services compile and unit tests pass. Ask the user if questions arise.

- [x] 11. Wire Up Publish Flow and Event Hooks
  - [x] 11.1 Hook AssignmentSnapshotService into PublishVersionCommand handler
    - Call `CreateSnapshotsAsync` after version is published, before counter update
    - Pass version ID and space ID
    - _Requirements: 3.1_
  - [x] 11.2 Hook CumulativeTracker into PublishVersionCommand handler
    - Call `UpdateOnPublishAsync` after snapshots are created
    - Pass version ID, space ID, and snapshot diff
    - _Requirements: 1.1, 2.1_
  - [x] 11.3 Hook CumulativeTracker.RecomputeForPersonAsync into presence-window commands
    - On PresenceWindow create/update/delete with state AtHome, call RecomputeForPersonAsync for affected person
    - _Requirements: 8.1, 8.2, 8.3, 8.4_
  - [x] 11.4 Hook PeriodManager into subscription status change handlers
    - On subscription transition to Active/Trialing: call OpenPeriodAsync
    - On subscription cancellation after 14-day grace: call ClosePeriodAsync
    - _Requirements: 5.1, 5.2, 5.3_
  - [x] 11.5 Hook CumulativeTracker.RecomputeForPersonAsync into version rollback
    - On schedule version rollback, recompute from presence_windows rather than decrementing
    - _Requirements: 1.6_

- [x] 12. Solver Payload Extension
  - [x] 12.1 Extend `SolverPayloadNormalizer` to include `cumulative_tracking` section
    - Call `ICumulativeTracker.GetForSolverPayloadAsync` during payload build
    - Add `cumulative_tracking` list to the solver input DTO
    - Map CumulativeTrackingDto list to JSON payload section
    - _Requirements: 1.3, 2.2, 6.1_
  - [ ]* 12.2 Write property test: Solver payload cumulative completeness (FsCheck)
    - **Property 3: Solver Payload Cumulative Completeness**
    - For any group with N persons having cumulative records, verify payload contains exactly N entries with all required fields matching stored values
    - **Validates: Requirements 1.3, 2.2, 6.1**

- [x] 13. Checkpoint — Publish flow and payload verification
  - Ensure publish creates snapshots, updates counters, and solver payload includes cumulative_tracking. Ask the user if questions arise.

- [x] 14. Python Solver Extension
  - [x] 14.1 Add `CumulativeTracking` Pydantic model to `solver_input.py`
    - Define model with fields: person_id, consecutive_hours_at_base, last_home_leave_end, total_assignments_in_period, hard_tasks_in_period, days_since_last_leave
    - Add `cumulative_tracking: list[CumulativeTracking] = []` to SolverInput model
    - _Requirements: 6.1, 6.4_
  - [x] 14.2 Modify home-leave eligibility to use cumulative hours
    - In `home_leave.py`, add consecutive_hours_at_base from cumulative_tracking to hours computed within current horizon
    - Compare (cumulative + horizon hours) against eligibility_threshold_hours
    - If cumulative_tracking is missing for a person, treat as zero (current behavior)
    - _Requirements: 1.4, 6.2, 6.4_
  - [x] 14.3 Modify fairness objectives to incorporate cumulative history
    - In `objectives.py`, add total_assignments_in_period as a bias term in the fairness penalty function
    - Apply configurable decay factor (default: use raw value as additive bias)
    - Persons with higher cumulative assignments receive higher penalty for additional hard tasks
    - _Requirements: 2.3, 6.3_
  - [ ]* 14.4 Write property test: Eligibility threshold with cumulative hours (Hypothesis)
    - **Property 4: Eligibility Threshold with Cumulative Hours**
    - For any person with consecutive_hours_at_base=C and horizon FreeInBase hours=H, verify eligibility iff (C+H) >= threshold
    - **Validates: Requirements 1.4, 6.2**
  - [ ]* 14.5 Write property test: Fairness penalty incorporates cumulative history (Hypothesis)
    - **Property 14: Fairness Penalty Incorporates Cumulative History**
    - For any two persons where A has higher total_assignments_in_period than B, verify penalty for assigning additional hard task to A >= penalty for B (all else equal)
    - **Validates: Requirements 2.3, 6.3**

- [x] 15. Checkpoint — Solver extension verification
  - Ensure solver parses cumulative_tracking, eligibility uses cumulative hours, and fairness incorporates history. Ask the user if questions arise.

- [x] 16. Stats API Endpoints
  - [x] 16.1 Create cumulative stats endpoint `GET /spaces/{spaceId}/stats/cumulative`
    - Accept query params: time_range (7d|14d|30d|90d|period), group_id, period_id
    - Return per-person statistics: total_assignments, hard_tasks, kitchen_count, night_missions, disliked_hated_score, total_hours_assigned, average_daily_burden
    - Default to current active period when no period_id specified
    - Enforce space_id tenant isolation
    - Require appropriate permission check
    - _Requirements: 4.2, 4.4, 4.5, 5.5, 5.6, 7.1, 7.3, 7.5_
  - [x] 16.2 Create time-series endpoint `GET /spaces/{spaceId}/stats/timeseries`
    - Accept query params: start_date, end_date, group_id, period_id
    - Return daily data points: date, assignments_count, total_burden, per-person breakdown
    - Scope to current period unless cross-period explicitly requested
    - Include period metadata in response
    - _Requirements: 7.2, 7.3, 7.4_
  - [x] 16.3 Create historical schedule endpoint `GET /spaces/{spaceId}/schedule/history`
    - Accept query params: group_id, start_date, end_date
    - Query daily_snapshots for the date range
    - Check schedule_history_retention_days setting; return empty with retention_exceeded flag if outside window
    - Return assignments in same format as current schedule endpoint
    - _Requirements: 9.1, 9.3, 9.4, 9.5_
  - [ ]* 16.4 Write property test: Stats query returns correctly scoped data (FsCheck)
    - **Property 11: Stats Query Returns Correctly Scoped Data**
    - For any stats query with time-range and filters, verify returned data includes only matching snapshots and never crosses space_id boundaries
    - **Validates: Requirements 4.2, 4.5, 5.5, 5.6, 7.3, 7.5**
  - [ ]* 16.5 Write property test: Retention limit enforcement (FsCheck)
    - **Property 13: Retention Limit Enforcement**
    - For any group with retention_days=R, verify queries for dates older than (today - R) return empty, and dates within window return data normally
    - **Validates: Requirements 9.3, 9.4**

- [x] 17. Checkpoint — API verification
  - Ensure stats and history endpoints return correct data with proper tenant isolation. Ask the user if questions arise.

- [x] 18. Frontend — Historical Schedule Viewing
  - [x] 18.1 Add API client functions for new endpoints
    - Add `getHistoricalSchedule(spaceId, groupId, startDate, endDate)` function
    - Add `getCumulativeStats(spaceId, groupId, timeRange, periodId?)` function
    - Add `getStatsTimeseries(spaceId, groupId, startDate, endDate, periodId?)` function
    - Define TypeScript interfaces for response types
    - _Requirements: 7.1, 7.2, 9.1_
  - [x] 18.2 Update schedule tab to fetch historical data for past dates
    - When user navigates to a past week, call `getHistoricalSchedule` instead of live schedule endpoint
    - Render historical assignments in the same grid format as current schedule
    - Show a banner/indicator when viewing a past date (e.g., "צפייה בהיסטוריה — שבוע X")
    - Handle retention_exceeded response with appropriate message
    - _Requirements: 9.1, 9.2, 9.5, 9.6_
  - [x] 18.3 Add time-range selector to stats page
    - Add selector with options: 7 ימים, 14 ימים, 30 ימים, 90 ימים, כל התקופה
    - Wire selector to `getCumulativeStats` with appropriate time_range param
    - Display per-person statistics table with all counter columns
    - _Requirements: 4.2, 7.1_

- [x] 19. Final checkpoint — Full integration verification
  - Ensure all tests pass, full publish flow creates snapshots and updates counters, solver uses cumulative data, stats API responds correctly, and frontend displays historical schedules. Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- FsCheck is used for C# property tests (domain logic, snapshot service, stats queries)
- Hypothesis is used for Python solver property tests (eligibility, fairness)
- The 5-phase migration strategy ensures safe rollback: all new tables are additive, solver payload extension is optional (`cumulative_tracking: list = []`)
- Backfill script (Phase 2) should be run once after schema migration and before wiring up the application layer
- Tenant isolation (space_id filtering + RLS) is enforced on all new tables per security rules

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4"] },
    { "id": 2, "tasks": ["4.1", "4.2"] },
    { "id": 3, "tasks": ["5.1"] },
    { "id": 4, "tasks": ["5.2", "5.3"] },
    { "id": 5, "tasks": ["5.4", "7.1"] },
    { "id": 6, "tasks": ["7.2", "7.3", "7.4", "7.5", "8.1"] },
    { "id": 7, "tasks": ["8.2", "8.3", "8.4", "8.5"] },
    { "id": 8, "tasks": ["8.6", "8.7", "8.8", "8.9", "8.10", "9.1"] },
    { "id": 9, "tasks": ["9.2", "11.1", "11.2", "11.3", "11.4", "11.5"] },
    { "id": 10, "tasks": ["12.1", "12.2"] },
    { "id": 11, "tasks": ["14.1"] },
    { "id": 12, "tasks": ["14.2", "14.3"] },
    { "id": 13, "tasks": ["14.4", "14.5"] },
    { "id": 14, "tasks": ["16.1", "16.2", "16.3"] },
    { "id": 15, "tasks": ["16.4", "16.5"] },
    { "id": 16, "tasks": ["18.1"] },
    { "id": 17, "tasks": ["18.2", "18.3"] }
  ]
}
```
