# Requirements Document

## Introduction

Split-Burden Scaling adjusts the effective burden level of task assignments based on how many sub-shifts a task is split into. When a task with a long duration (≥ 4 hours original) is split into shorter segments, each segment represents less time per person, making it objectively easier. This feature reflects that reality by automatically decreasing the tracked burden level for each split level applied. The adjustment happens post-solver — the solver continues to use the original burden level for balancing, and the displayed/tracked burden level is computed after assignments are resolved.

## Glossary

- **GroupTask**: A recurring task entity defining a time window, shift duration, headcount, and burden level. The solver fills the window with shifts of `ShiftDurationMinutes` length.
- **TaskBurdenLevel**: An enum with three ordered tiers: `Easy` (0), `Normal` (1), `Hard` (2).
- **Sub-Shift**: The result of splitting a task's original shift duration into N equal parts via the SubShiftEditor. Splitting divides `ShiftDurationMinutes` by N.
- **Split_Count**: The number of sub-shifts a task is divided into (N). A value of 1 means no split.
- **Original_Duration**: The task's shift duration before any splitting is applied (the base duration when Split_Count = 1).
- **Effective_Burden_Level**: The burden level after applying the split-based reduction. Computed as `max(Easy, Original_BurdenLevel - (Split_Count - 1))`.
- **Solver**: The Python CP-SAT scheduling engine that assigns people to shifts. It receives burden levels in its input payload and uses them for fairness balancing.
- **DailySnapshot**: An immutable per-person-per-day record of an assignment, storing the burden level for historical tracking and statistics.
- **AssignmentSnapshotService**: The service that resolves slot metadata (including burden level) and creates DailySnapshot records after a schedule version is published.
- **FairnessCounter**: Aggregated per-person statistics (7-day window) used by the solver for fairness. Includes `HardTasks7d` count.

## Requirements

### Requirement 1: Split Count Persistence

**User Story:** As an admin, I want the system to track how many sub-shifts a task is split into, so that the burden scaling can be computed from the split count.

#### Acceptance Criteria

1. WHEN an admin splits a task into N sub-shifts via the SubShiftEditor, THE GroupTask SHALL persist both the resulting `ShiftDurationMinutes` and the `Split_Count` (N).
2. WHEN a task has not been split, THE GroupTask SHALL store a `Split_Count` of 1.
3. WHEN an admin merges sub-shifts back (reducing N), THE GroupTask SHALL update the `Split_Count` to the new value.

### Requirement 2: Minimum Duration Threshold

**User Story:** As an admin, I want burden scaling to only apply to tasks with sufficiently long original durations, so that short tasks are not affected by the scaling logic.

#### Acceptance Criteria

1. THE Burden_Scaling_Service SHALL only apply burden reduction to tasks whose Original_Duration (ShiftDurationMinutes × Split_Count) is greater than or equal to 240 minutes (4 hours).
2. WHEN a task's Original_Duration is less than 240 minutes, THE Burden_Scaling_Service SHALL use the task's original burden level without modification.

### Requirement 3: Burden Level Reduction Formula

**User Story:** As an admin, I want each split level to reduce the effective burden by one tier, so that shorter segments are tracked as less burdensome.

#### Acceptance Criteria

1. THE Burden_Scaling_Service SHALL compute the Effective_Burden_Level as: `max(Easy, Original_BurdenLevel - (Split_Count - 1))`.
2. WHEN a task with `BurdenLevel = Hard` is split into 2 sub-shifts, THE Burden_Scaling_Service SHALL produce an Effective_Burden_Level of `Normal`.
3. WHEN a task with `BurdenLevel = Hard` is split into 3 or more sub-shifts, THE Burden_Scaling_Service SHALL produce an Effective_Burden_Level of `Easy`.
4. WHEN a task with `BurdenLevel = Normal` is split into 2 or more sub-shifts, THE Burden_Scaling_Service SHALL produce an Effective_Burden_Level of `Easy`.
5. WHEN a task with `BurdenLevel = Easy` is split (any Split_Count), THE Burden_Scaling_Service SHALL produce an Effective_Burden_Level of `Easy` (floor — no further reduction).

### Requirement 4: Solver Uses Original Burden Level

**User Story:** As a system operator, I want the solver to continue using the original (unsplit) burden level for its fairness balancing, so that the solver's optimization is not affected by display-level adjustments.

#### Acceptance Criteria

1. THE SolverPayloadNormalizer SHALL send the original `BurdenLevel` from the GroupTask (not the effective burden level) in the solver input payload.
2. WHEN building TaskSlotDto entries for group tasks, THE SolverPayloadNormalizer SHALL use `task.BurdenLevel.ToString().ToLower()` without applying split-based reduction.

### Requirement 5: Post-Solver Burden Adjustment in Snapshots

**User Story:** As an admin, I want the daily assignment snapshots to reflect the split-adjusted burden level, so that historical statistics and the burden dashboard show the effective burden each person experienced.

#### Acceptance Criteria

1. WHEN the AssignmentSnapshotService resolves the burden level for a DailySnapshot, THE AssignmentSnapshotService SHALL use the Effective_Burden_Level (split-adjusted) instead of the raw task burden level.
2. WHEN a GroupTask has a Split_Count greater than 1 and an Original_Duration of at least 240 minutes, THE AssignmentSnapshotService SHALL store the reduced burden level in the DailySnapshot.
3. WHEN a GroupTask has a Split_Count of 1, THE AssignmentSnapshotService SHALL store the original burden level in the DailySnapshot (no change from current behavior).

### Requirement 6: Fairness Counter Adjustment

**User Story:** As a system operator, I want the fairness counters to reflect the effective burden level, so that the `HardTasks7d` metric accurately represents the actual burden experienced by each person.

#### Acceptance Criteria

1. WHEN the UpdateFairnessCountersCommand counts hard tasks for a person, THE UpdateFairnessCountersCommand SHALL use the Effective_Burden_Level to determine whether an assignment counts as "hard".
2. WHEN a task was originally `Hard` but its Effective_Burden_Level is `Normal` due to splitting, THE UpdateFairnessCountersCommand SHALL NOT count that assignment toward `HardTasks7d`.

### Requirement 7: UI Display of Effective Burden

**User Story:** As an admin, I want the schedule view and statistics pages to display the effective (split-adjusted) burden level, so that I can see the actual burden each person bears.

#### Acceptance Criteria

1. WHEN displaying an assignment's burden level in the schedule grid, THE Frontend SHALL show the Effective_Burden_Level (as stored in the DailySnapshot).
2. WHEN displaying burden statistics and leaderboards, THE Frontend SHALL use the Effective_Burden_Level from DailySnapshot records for all calculations.
3. WHEN displaying a task's configuration in the task list, THE Frontend SHALL show both the original burden level and the effective burden level when Split_Count is greater than 1.

### Requirement 8: Burden Level in PDF/Excel Exports

**User Story:** As an admin, I want exported schedules to show the effective burden level, so that printed or shared schedules reflect the split-adjusted values.

#### Acceptance Criteria

1. WHEN generating a PDF or Excel export of a published schedule, THE ExportService SHALL use the Effective_Burden_Level (from DailySnapshot) for the burden column.
