# Requirements Document

## Introduction

Overhaul the statistics system to provide comprehensive, per-person tracking of assignment burden over time. This includes changing the burden level taxonomy from 4 levels (Favorable, Neutral, Disliked, Hated) to 3 levels (Easy, Normal, Hard), adding graphical visualizations, color-coding missions by difficulty, and introducing task rotation tracking for army-template groups. The change affects the domain model, solver, API, and frontend.

## Glossary

- **Statistics_Service**: The backend service responsible for computing and serving per-person assignment statistics, fairness counters, and historical trends.
- **Statistics_View**: The frontend component that displays statistics data including leaderboards, tables, and graphs.
- **Burden_Level**: A difficulty classification assigned to each task type. New taxonomy: Hard, Normal, Easy.
- **Fairness_Counter**: A rolling ledger per person tracking assignment counts and burden scores over configurable time windows.
- **Task_Rotation_Tracker**: A component that monitors whether each person in an army-template group has cycled through all task types they are qualified for.
- **Solver**: The Python CP-SAT scheduling engine that assigns people to task slots while optimizing for fairness.
- **Space**: A tenant-scoped workspace containing people, groups, tasks, and schedules.
- **Army_Template_Group**: A group configured with the army template, which enforces strict task rotation rules.

## Requirements

### Requirement 1: Burden Level Taxonomy Change

**User Story:** As a space admin, I want the burden classification to use three levels (Hard, Normal, Easy) instead of four, so that the difficulty model is simpler and more intuitive.

#### Acceptance Criteria

1. THE Statistics_Service SHALL classify task types using exactly three burden levels: Hard, Normal, and Easy.
2. WHEN a Space contains task types with legacy burden levels (Favorable, Neutral, Disliked, Hated), THE Statistics_Service SHALL map them to the new taxonomy using the following rules: Hated maps to Hard, Disliked maps to Hard, Neutral maps to Normal, Favorable maps to Easy.
3. THE Solver SHALL use the burden levels Hard, Normal, and Easy as input for fairness penalty calculations.
4. WHEN the burden level enum is updated, THE Statistics_Service SHALL provide a database migration that converts all existing TaskBurdenLevel values to the new taxonomy without data loss.
5. THE Statistics_Service SHALL expose the new burden levels through the API using the string values "hard", "normal", and "easy".

### Requirement 2: Comprehensive Per-Person Statistics Tracking

**User Story:** As a space admin, I want to see detailed per-person statistics over time, so that I can identify who is being unfairly burdened.

#### Acceptance Criteria

1. THE Statistics_Service SHALL track the following metrics per person: total assignments, hard task count, easy task count, burden score, consecutive hard assignments, vacation days, home leave days, and sick days.
2. THE Statistics_Service SHALL compute metrics over configurable time windows: 7 days, 14 days, 30 days, and all-time.
3. WHEN a schedule version is published, THE Statistics_Service SHALL update the Fairness_Counter for all affected people within 60 seconds.
4. THE Statistics_Service SHALL persist historical Fairness_Counter snapshots so that trends can be computed across any date range.
5. WHEN statistics are requested for a group, THE Statistics_Service SHALL return only metrics for members of that group.
6. THE Statistics_Service SHALL compute a burden score using the formula: (hard_task_count × 3) + (normal_task_count × 0) − (easy_task_count × 1).

### Requirement 3: Color-Coded Mission Display

**User Story:** As a user viewing statistics, I want missions color-coded by difficulty, so that I can visually distinguish hard, normal, and easy assignments at a glance.

#### Acceptance Criteria

1. THE Statistics_View SHALL display Hard missions with a red color indicator (hex #dc2626).
2. THE Statistics_View SHALL display Normal missions with a gray color indicator (hex #6b7280).
3. THE Statistics_View SHALL display Easy missions with a green color indicator (hex #16a34a).
4. WHEN a mission list or assignment breakdown is shown, THE Statistics_View SHALL apply the color indicator to each mission entry based on its burden level.
5. THE Statistics_View SHALL include a legend explaining the color-to-difficulty mapping.

### Requirement 4: Graphical Statistics Visualization

**User Story:** As a space admin, I want statistics displayed as graphs, so that I can quickly understand trends and compare people visually.

#### Acceptance Criteria

1. THE Statistics_View SHALL display a bar chart comparing total assignments per person within a selected time window.
2. THE Statistics_View SHALL display a stacked bar chart showing the breakdown of Hard, Normal, and Easy assignments per person.
3. THE Statistics_View SHALL display a line chart showing burden score trends over time per person.
4. THE Statistics_View SHALL allow the user to select a time range for graph data using predefined options: 7 days, 14 days, 30 days, 90 days, and all-time.
5. THE Statistics_View SHALL display a fairness comparison chart showing how each person's burden score deviates from the group average.
6. WHEN fewer than two published schedule versions exist, THE Statistics_View SHALL display a placeholder message instead of trend graphs.

### Requirement 5: Historical Statistics API

**User Story:** As a frontend developer, I want a time-series statistics API, so that I can render graphs with historical data points.

#### Acceptance Criteria

1. THE Statistics_Service SHALL expose an endpoint that returns daily aggregated statistics per person for a requested date range.
2. WHEN a date range is requested, THE Statistics_Service SHALL return one data point per day per person containing: date, total assignments, hard count, normal count, easy count, and burden score.
3. THE Statistics_Service SHALL support filtering historical data by group membership.
4. IF the requested date range exceeds 365 days, THEN THE Statistics_Service SHALL return a 400 error with a descriptive message.
5. THE Statistics_Service SHALL return historical data sorted by date in ascending order.

### Requirement 6: Task Rotation Tracking (Army Template)

**User Story:** As an army-template group admin, I want to track whether each person has rotated through all task types they are qualified for, so that no one misses any duty.

#### Acceptance Criteria

1. WHILE a group is configured with the army template, THE Task_Rotation_Tracker SHALL maintain a record of which task types each person has completed.
2. THE Task_Rotation_Tracker SHALL compute a rotation completion percentage per person: (distinct task types completed / total task types the person is qualified for) × 100.
3. WHEN a person has completed all qualified task types, THE Task_Rotation_Tracker SHALL reset their rotation cycle and begin tracking the next cycle.
4. THE Statistics_View SHALL display rotation progress per person in army-template groups as a progress indicator.
5. THE Task_Rotation_Tracker SHALL provide rotation data to the Solver so that the Solver can prioritize assigning uncompleted task types.
6. IF a person's qualifications change mid-cycle, THEN THE Task_Rotation_Tracker SHALL recalculate the rotation denominator without resetting completed task types.

### Requirement 7: Solver Fairness Integration with New Burden Levels

**User Story:** As a system operator, I want the solver to use the new 3-level burden taxonomy for fairness optimization, so that scheduling decisions reflect the updated difficulty model.

#### Acceptance Criteria

1. THE Solver SHALL accept burden levels as "hard", "normal", and "easy" in the task slot payload.
2. THE Solver SHALL apply fairness penalties using the weights: hard = 4, normal = 0, easy = -1.
3. WHEN historical fairness counters reference legacy burden levels, THE Solver SHALL interpret them using the same mapping as Requirement 1 acceptance criterion 2.
4. THE Solver SHALL include task rotation data in its fairness objective for army-template groups.
5. IF a task slot has no burden level specified, THEN THE Solver SHALL default to "normal" with a penalty weight of 0.

### Requirement 8: Migration Strategy for Burden Level Change

**User Story:** As a system operator, I want the burden level change to be backward-compatible during rollout, so that existing schedules and data are not corrupted.

#### Acceptance Criteria

1. THE Statistics_Service SHALL provide a reversible database migration that renames burden level values in all affected tables (task_types, group_tasks, fairness_counters).
2. THE Statistics_Service SHALL update the SolverPayloadNormalizer to emit the new burden level strings in solver input payloads.
3. WHEN the migration runs, THE Statistics_Service SHALL log the count of records migrated per table.
4. THE Solver SHALL accept both legacy burden level strings and new burden level strings during a transition period, mapping legacy values using the rules from Requirement 1.
5. IF a rollback is required, THEN THE Statistics_Service SHALL provide a reverse migration that restores the original 4-level taxonomy using the mapping: Hard → Hated (for previously-Hated) or Hard → Disliked (for previously-Disliked), Normal → Neutral, Easy → Favorable.
