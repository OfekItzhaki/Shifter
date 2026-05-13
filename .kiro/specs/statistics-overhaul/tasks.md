# Implementation Plan: Statistics Overhaul

## Overview

Overhaul the statistics system in 5 phases: (1) rename the burden level taxonomy from 4 levels to 3, (2) expand the fairness counter with new metrics and historical snapshots, (3) add task rotation tracking for army-template groups, (4) build frontend graphs and visualizations with Recharts, and (5) optional property-based tests. Each phase builds incrementally — Phase 1 is the most critical as it touches domain, database, solver, and frontend.

## Tasks

- [ ] 1. Phase 1: Burden Level Migration — Domain + Database + Solver
  - [ ] 1.1 Rename `TaskBurdenLevel` enum values in the domain layer
    - Update `TaskBurdenLevel.cs` to have exactly 3 members: `Easy`, `Normal`, `Hard`
    - Remove `Favorable`, `Neutral`, `Disliked`, `Hated` values
    - Update any domain methods that reference the old enum values
    - _Requirements: 1.1, 1.2_
  - [ ] 1.2 Create database migration to rename burden level values
    - Create a new SQL migration file (next sequential number)
    - UPDATE `task_types` SET burden_level: Hated→Hard, Disliked→Hard, Neutral→Normal, Favorable→Easy
    - UPDATE `group_tasks` SET burden_level: same mapping
    - UPDATE `fairness_counters` columns: rename `hated_tasks_7d`→`hard_tasks_7d`, `hated_tasks_14d`→`hard_tasks_14d`, rename `consecutive_burden_count`→`consecutive_hard_count`
    - Log record counts before and after migration
    - _Requirements: 1.4, 8.1, 8.3_
  - [ ] 1.3 Update EF Core enum conversion configuration
    - Update `TaskBurdenLevel` string conversion in EF Core configuration to map "Easy", "Normal", "Hard"
    - Update any entity configurations referencing the old enum values
    - _Requirements: 1.1, 1.5_
  - [ ] 1.4 Update `SolverPayloadNormalizer` to emit new burden level strings
    - Change the normalizer to output "hard", "normal", "easy" instead of legacy strings
    - Ensure task slot payloads use the new taxonomy
    - _Requirements: 8.2_
  - [ ] 1.5 Update Python solver `burden_map` in `objectives.py` with backward compatibility
    - Add new keys: "hard"→4, "normal"→0, "easy"→-1
    - Keep legacy keys: "hated"→4, "disliked"→4, "neutral"→0, "favorable"→-1
    - Default unknown burden levels to "normal" (weight 0) with a warning log
    - _Requirements: 7.1, 7.2, 7.3, 7.5, 8.4_
  - [ ] 1.6 Update frontend burden labels and color constants
    - Create/update burden constants file with `burdenLabels` and `burdenColors` maps
    - Hard → red (#dc2626), Normal → gray (#6b7280), Easy → green (#16a34a)
    - Update any existing UI references to old burden level names
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [ ] 2. Checkpoint — Phase 1 verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify the migration runs cleanly against a local database.
  - Verify the solver accepts both old and new burden level strings.

- [ ] 3. Phase 2: Enhanced Statistics Backend
  - [ ] 3.1 Expand `FairnessCounter` entity with new fields
    - Add properties: `TotalAssignments7d/14d/30d`, `HardTasks30d`, `EasyTasks7d/14d/30d`, `BurdenScore7d/14d/30d`, `ConsecutiveHardCount`
    - Add `Update()` method that validates all counts are non-negative
    - _Requirements: 2.1, 2.2, 2.6_
  - [ ] 3.2 Create database migration for `fairness_counter_snapshots` table
    - Create `fairness_counter_snapshots` table with columns: id, space_id, person_id, snapshot_date, total_assignments, hard_count, normal_count, easy_count, burden_score, created_at
    - Add UNIQUE constraint on (space_id, person_id, snapshot_date)
    - Add index on (space_id, snapshot_date)
    - Add new columns to `fairness_counters`: hard_tasks_30d, easy_tasks_7d/14d/30d, burden_score_7d/14d/30d
    - _Requirements: 2.1, 2.4, 5.1_
  - [ ] 3.3 Update `UpdateFairnessCountersCommand` to compute new metrics and persist snapshots
    - Compute metrics per time window (7d, 14d, 30d) using assignment start dates
    - Calculate burden score: (hard×3) + (normal×0) − (easy×1)
    - Persist a daily snapshot row in `fairness_counter_snapshots` after each update
    - _Requirements: 2.2, 2.3, 2.4, 2.6_
  - [ ] 3.4 Create `GetHistoricalPersonStatsQuery` and handler
    - Accept SpaceId, StartDate, EndDate, optional GroupId
    - Validate date range ≤ 365 days (return 400 if exceeded)
    - Validate StartDate < EndDate
    - Query `fairness_counter_snapshots` filtered by space, date range, and optional group
    - Return data sorted by date ascending
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_
  - [ ] 3.5 Create historical stats API endpoint in `StatsController`
    - Add `GET /spaces/{spaceId}/stats/historical/persons` endpoint
    - Accept query params: startDate, endDate, groupId (optional)
    - Wire to `GetHistoricalPersonStatsQuery` via MediatR
    - Add `[Authorize]` and permission check
    - _Requirements: 5.1, 5.2, 5.3_

- [ ] 4. Checkpoint — Phase 2 verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify the historical endpoint returns correct data shape.

- [ ] 5. Phase 3: Task Rotation (Army Template)
  - [ ] 5.1 Create `TaskRotationProgress` entity and database table
    - Create domain entity with: SpaceId, PersonId, GroupId, CycleNumber, CompletedTaskTypeIds, TotalQualifiedTaskTypes, CompletionPercentage, LastUpdatedAt
    - Implement `ITenantScoped`
    - Create SQL migration for `task_rotation_progress` table with UNIQUE(space_id, person_id, group_id) and index on group_id
    - Add EF Core configuration
    - _Requirements: 6.1, 6.2_
  - [ ] 5.2 Create `ComputeTaskRotationCommand` and handler
    - Accept SpaceId and GroupId
    - For each person in the group: count distinct completed task types from assignments, compute completion percentage
    - Handle cycle reset: when all qualified types completed, increment cycle_number and reset completed list
    - Handle qualification changes: recalculate denominator without resetting completed types
    - _Requirements: 6.1, 6.2, 6.3, 6.6_
  - [ ] 5.3 Create rotation API endpoint
    - Add `GET /spaces/{spaceId}/stats/rotation?groupId={id}` endpoint in `StatsController`
    - Validate group exists and is army-template (return 404 if not)
    - Add `[Authorize]` and permission check
    - Return rotation progress per person
    - _Requirements: 6.4_
  - [ ] 5.4 Integrate rotation data into solver fairness objective
    - Include rotation progress in solver payload for army-template groups
    - Update `SolverPayloadNormalizer` to include rotation data
    - Update solver `objectives.py` to add penalty for assigning already-completed task types in current cycle
    - _Requirements: 6.5, 7.4_

- [ ] 6. Checkpoint — Phase 3 verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify rotation endpoint returns correct progress data for army-template groups.

- [ ] 7. Phase 4: Frontend Graphs + Visualization
  - [ ] 7.1 Install Recharts dependency
    - Add `recharts` package to the frontend app
    - Verify TypeScript types are available
    - _Requirements: 4.1_
  - [ ] 7.2 Create bar chart component (total assignments per person)
    - Create a reusable `AssignmentsBarChart` component using Recharts `BarChart`
    - Accept data array with person name and total assignments
    - Support time-window prop for label display
    - _Requirements: 4.1_
  - [ ] 7.3 Create stacked bar chart component (hard/normal/easy breakdown)
    - Create `BurdenBreakdownChart` component using Recharts `BarChart` with stacked bars
    - Use burden colors: red for hard, gray for normal, green for easy
    - Accept data array with person name and per-level counts
    - _Requirements: 4.2_
  - [ ] 7.4 Create line chart component (burden score trend over time)
    - Create `BurdenTrendChart` component using Recharts `LineChart`
    - Accept historical data points (date + burden score per person)
    - Support multi-line display (one line per person)
    - _Requirements: 4.3_
  - [ ] 7.5 Create fairness comparison chart
    - Create `FairnessComparisonChart` component showing deviation from group average
    - Display each person's burden score relative to the group mean
    - Use positive/negative bar or diverging bar chart
    - _Requirements: 4.5_
  - [ ] 7.6 Update StatsTab to use graphs and add time range selector
    - Add time range selector with options: 7d, 14d, 30d, 90d, all-time
    - Fetch historical data from the new API endpoint based on selected range
    - Render all chart components with fetched data
    - Show placeholder message when fewer than 2 published schedule versions exist
    - _Requirements: 4.4, 4.6_
  - [ ] 7.7 Add color badges for burden levels in assignment lists
    - Create a `BurdenBadge` component that renders a colored pill/tag
    - Apply to mission lists and assignment breakdowns in the stats view
    - Include a legend explaining color-to-difficulty mapping
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_
  - [ ] 7.8 Add rotation progress display for army-template groups
    - Create a `RotationProgressCard` component showing per-person completion percentage
    - Display cycle number and progress bar
    - Fetch data from the rotation API endpoint
    - Only show for groups configured with army template
    - _Requirements: 6.4_

- [ ] 8. Checkpoint — Phase 4 verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify graphs render correctly with sample data.

- [ ] 9. Phase 5: Property-Based Tests (Optional)
  - [ ]* 9.1 Write property test: Legacy Burden Level Mapping
    - **Property 1: Legacy Burden Level Mapping**
    - Test that for any legacy value {Hated, Disliked, Neutral, Favorable}, the mapping produces the correct new value
    - Use FsCheck in .NET test project
    - **Validates: Requirements 1.2**
  - [ ]* 9.2 Write property test: Burden Level Serialization round-trip
    - **Property 2: Burden Level Serialization**
    - Test that serializing any TaskBurdenLevel to string and back produces the original value
    - Use FsCheck in .NET test project
    - **Validates: Requirements 1.5, 8.2**
  - [ ]* 9.3 Write property test: Solver Penalty Weight Application
    - **Property 3: Solver Penalty Weight Application**
    - Test that penalty lookup returns 4 for "hard", 0 for "normal", -1 for "easy"
    - Use Hypothesis in Python test file
    - **Validates: Requirements 1.3, 7.2**
  - [ ]* 9.4 Write property test: Solver Backward-Compatible Burden Mapping
    - **Property 4: Solver Backward-Compatible Burden Mapping**
    - Test all 7 valid strings map to correct weights (legacy + new)
    - Use Hypothesis in Python test file
    - **Validates: Requirements 7.3, 8.4**
  - [ ]* 9.5 Write property test: Time-Window Filtering
    - **Property 5: Time-Window Filtering**
    - Generate random assignment sets with timestamps, verify only those within window are counted
    - Use FsCheck in .NET test project
    - **Validates: Requirements 2.2**
  - [ ]* 9.6 Write property test: Burden Score Formula
    - **Property 7: Burden Score Formula**
    - For any non-negative (hard, normal, easy) counts, verify score = (hard×3) − (easy×1)
    - Use FsCheck in .NET test project
    - **Validates: Requirements 2.6**
  - [ ]* 9.7 Write property test: Color Mapping Completeness
    - **Property 8: Color Mapping Completeness**
    - For any valid burden level string, verify a non-empty color is returned
    - Use fast-check in TypeScript test file
    - **Validates: Requirements 3.4**
  - [ ]* 9.8 Write property test: Rotation Completion Percentage
    - **Property 11: Rotation Completion Percentage**
    - For any (completed, total) where completed ≤ total and total > 0, verify percentage = (completed/total)×100
    - Use FsCheck in .NET test project
    - **Validates: Requirements 6.2**
  - [ ]* 9.9 Write property test: Cycle Reset on Full Completion
    - **Property 12: Cycle Reset on Full Completion**
    - When completed count equals total, verify cycle increments and completed list resets
    - Use FsCheck in .NET test project
    - **Validates: Requirements 6.3**

- [ ] 10. Final Checkpoint
  - Ensure all tests pass, ask the user if questions arise.
  - Verify end-to-end: burden levels display correctly, graphs render, rotation tracking works.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Phase 1 (burden level rename) is the most critical — it touches many files but each change is small
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation between phases
- The solver backward-compatibility in task 1.5 allows rolling deployment without downtime
- Property tests validate universal correctness properties from the design document
- Step documentation files should be created per the workspace steering rules as each phase is completed

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["1.4", "1.5", "1.6"] },
    { "id": 3, "tasks": ["3.1", "3.2"] },
    { "id": 4, "tasks": ["3.3", "3.4"] },
    { "id": 5, "tasks": ["3.5"] },
    { "id": 6, "tasks": ["5.1"] },
    { "id": 7, "tasks": ["5.2", "5.3"] },
    { "id": 8, "tasks": ["5.4"] },
    { "id": 9, "tasks": ["7.1"] },
    { "id": 10, "tasks": ["7.2", "7.3", "7.4", "7.5", "7.7"] },
    { "id": 11, "tasks": ["7.6", "7.8"] },
    { "id": 12, "tasks": ["9.1", "9.2", "9.3", "9.4", "9.5", "9.6", "9.7", "9.8", "9.9"] }
  ]
}
```
