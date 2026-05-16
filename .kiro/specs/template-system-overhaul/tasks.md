# Implementation Plan: Template System Overhaul

## Overview

Transform the Shifter platform from hardcoded domain assumptions into a truly generic scheduling system. This involves: persisting `TemplateType` on the Group entity, creating a frontend feature visibility map, removing dead code (DislikedHatedScore, KitchenCount), generalizing `max_kitchen_per_week` → `max_task_type_per_period`, removing `IsKitchenTask()`, removing `min_rest_hours` from template seed data, and adding template-aware labels for the closed-base/stayover feature.

## Tasks

- [x] 1. Database migration and domain model changes
  - [x] 1.1 Create database migration `056_template_system_overhaul.sql`
    - Add `template_type TEXT NOT NULL DEFAULT 'Custom'` column to `groups` table
    - Drop `disliked_hated_score_7d/14d/30d/90d/period` columns from `cumulative_records`
    - Drop `kitchen_count_7d/14d/30d/90d/period` columns from `cumulative_records`
    - Add `task_type_counts JSONB NOT NULL DEFAULT '{}'` column to `cumulative_records`
    - Drop `disliked_hated_score_7d` and `kitchen_count_7d` columns from `fairness_counters`
    - Convert `max_kitchen_per_week` constraint rules to `max_task_type_per_period` with `task_type_name="kitchen"`, `period_days=7`
    - Add index `idx_groups_template_type` on `groups(template_type)`
    - _Requirements: 1.1, 1.2, 3.4, 4.4, 5.4_

  - [x] 1.2 Add `GroupTemplateType` enum and extend `Group` entity
    - Create `GroupTemplateType` enum in `Jobuler.Domain/Groups/` with values: Army, Restaurant, Hospital, Security, Custom
    - Add `TemplateType` property to `Group.cs` with default `Custom`
    - Add `SetTemplateType(GroupTemplateType)` method to `Group`
    - Add optional `templateType` parameter to `Group.Create()` factory method
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [x] 1.3 Remove dead fields from `CumulativeRecord` and `AssignmentCountsDelta`
    - Remove `DislikedHatedScore7d/14d/30d/90d/Period` properties from `CumulativeRecord.cs`
    - Remove `KitchenCount7d/14d/30d/90d/Period` properties from `CumulativeRecord.cs`
    - Add `TaskTypeCountsJson` (string, nullable) property to `CumulativeRecord.cs`
    - Remove `DislikedHatedScore` and `KitchenCount` from `AssignmentCountsDelta` record in `CumulativeValueObjects.cs`
    - Add `Dictionary<string, int>? TaskTypeCounts = null` parameter to `AssignmentCountsDelta`
    - Update `IncrementCounters` method to handle new structure (remove dead increments, add task-type JSON merge)
    - Update `ResetPeriodCounters` to reset `TaskTypeCountsJson`
    - _Requirements: 3.1, 3.2, 3.3, 5.1, 5.2, 5.5_

  - [x] 1.4 Remove dead fields from `FairnessCounter` entity and DTO
    - Remove `DislikedHatedScore7d` property from `FairnessCounter.cs`
    - Remove `KitchenCount7d` property from `FairnessCounter.cs`
    - Remove `kitchen7d` parameter from `FairnessCounter.Update()` method
    - Add `TaskTypeCountsJson` (string, nullable) property to `FairnessCounter`
    - Remove `DislikedHatedScore7d` and `KitchenCount7d` from `FairnessCountersDto` in `SolverInputDto.cs`
    - Add `TaskTypeCounts7d` (Dictionary<string, int>) to `FairnessCountersDto`
    - _Requirements: 3.5, 3.6, 5.2_

- [x] 2. Checkpoint — Schema and domain compilation
  - Ensure migration file is valid SQL, all domain entities compile without errors, and no references to removed fields remain in Domain layer. Ask the user if questions arise.

- [x] 3. Infrastructure layer changes — CumulativeTracker and SolverPayloadNormalizer
  - [x] 3.1 Remove `IsKitchenTask()` and generalize counting in `CumulativeTracker.cs`
    - Delete the `IsKitchenTask(string?)` method entirely
    - Replace `int isKitchen = IsKitchenTask(taskName) ? 1 : 0;` with generic task-type counting using a `Dictionary<string, int>` per person
    - Update delta construction to use new `AssignmentCountsDelta` signature (no DislikedHatedScore, no KitchenCount, add TaskTypeCounts)
    - Serialize task-type counts to JSONB when updating `CumulativeRecord.TaskTypeCountsJson`
    - _Requirements: 5.1, 5.3_

  - [x] 3.2 Remove dead fields from `SolverPayloadNormalizer.cs` fairness DTO construction
    - Update the `fairnessDto` construction to remove `f.DislikedHatedScore7d` and `f.KitchenCount7d`
    - Add `TaskTypeCounts7d` from the new `FairnessCounter.TaskTypeCountsJson` field (deserialize JSONB)
    - Update `FairnessCountersDto` constructor call to match new signature
    - _Requirements: 3.5, 5.2_

  - [x] 3.3 Update EF Core configurations for modified entities
    - Update `CumulativeRecordConfiguration` to remove mappings for dropped columns and add `task_type_counts` JSONB mapping
    - Update `FairnessCounterConfiguration` to remove `disliked_hated_score_7d` and `kitchen_count_7d` mappings, add `task_type_counts` mapping
    - Update `GroupConfiguration` to map `template_type` column to `TemplateType` property (stored as text)
    - _Requirements: 1.1, 3.4, 5.4_

  - [x] 3.4 Update `UpdateFairnessCountersCommand` handler
    - Remove computation of `kitchen7d` using `IsKitchenTask`-style logic
    - Replace with generic task-type counting per person (group assignments by task name)
    - Serialize task-type counts to JSON and store on `FairnessCounter.TaskTypeCountsJson`
    - Remove `kitchen7d` parameter from `FairnessCounter.Update()` call
    - _Requirements: 3.3, 5.1, 5.3_

- [x] 4. Checkpoint — Infrastructure compilation and dead code verification
  - Ensure all Infrastructure and Application layer code compiles. Grep for `DislikedHatedScore`, `KitchenCount`, `IsKitchenTask`, `max_kitchen_per_week` in source code (excluding migration and docs) — should find zero matches. Ask the user if questions arise.

- [ ] 5. Solver layer changes
  - [x] 5.1 Update `FairnessCounters` Pydantic model in `models/solver_input.py`
    - Remove `disliked_hated_score_7d` field
    - Remove `kitchen_count_7d` field
    - Add `task_type_counts_7d: dict[str, int] = {}` field
    - _Requirements: 3.6, 5.2_

  - [x] 5.2 Replace `add_kitchen_frequency_constraints` with `add_max_task_type_per_period_constraints` in `solver/constraints.py`
    - Delete the `add_kitchen_frequency_constraints` function entirely
    - Add new `add_max_task_type_per_period_constraints` function that:
      - Filters hard_constraints for `rule_type == "max_task_type_per_period"`
      - Extracts `task_type_name`, `max`, `period_days` from payload
      - Finds matching slots by `task_type_name` (case-insensitive)
      - Uses `fairness_counters[].task_type_counts_7d.get(task_type_name, 0)` for historical count
      - Constrains each person to at most `max(0, max_allowed - already_done)` new assignments
    - _Requirements: 4.1, 4.2, 4.5_

  - [x] 5.3 Update solver main entry point to call new constraint function
    - Replace call to `add_kitchen_frequency_constraints` with `add_max_task_type_per_period_constraints`
    - Ensure the new function receives `fairness_counters` parameter
    - _Requirements: 4.5_

  - [ ]* 5.4 Write property test: Solver max_task_type_per_period enforcement (Hypothesis)
    - **Property 2: Solver max_task_type_per_period enforcement**
    - For any valid solver input with a `max_task_type_per_period` constraint (task_type_name=T, max=M), and any person with historical count H, verify the solver assigns at most max(0, M-H) slots matching T to that person
    - **Validates: Requirements 4.2**

- [x] 6. Checkpoint — Solver verification
  - Ensure solver tests pass, `add_max_task_type_per_period_constraints` is called correctly, and no references to `max_kitchen_per_week` or `kitchen_count_7d` remain in solver code. Ask the user if questions arise.

- [x] 7. API layer changes
  - [x] 7.1 Update Group DTOs and controller to expose `templateType`
    - Add `TemplateType` field to `GroupResponseDto` (or equivalent response record)
    - Add optional `TemplateType` field to `CreateGroupRequest` and `UpdateGroupRequest`
    - Update `GroupsController` create/update handlers to pass `templateType` to domain
    - Add FluentValidation rule: `templateType` must be one of Army, Restaurant, Hospital, Security, Custom (or null for default)
    - _Requirements: 1.1, 1.4, 1.5_

  - [x] 7.2 Update `ConstraintsController` to accept `max_task_type_per_period`
    - Add `max_task_type_per_period` as a valid `ruleType` in constraint create/update
    - Add FluentValidation: payload must contain `task_type_name` (non-empty string), `max` (int > 0), `period_days` (int > 0)
    - _Requirements: 4.1, 4.3_

  - [x] 7.3 Remove `DislikedHatedScore` from stats query response DTOs
    - Update `GetCumulativeStatsQuery` response to remove DislikedHatedScore fields
    - Update `GetBurdenStatsQuery` response to remove DislikedHatedScore references
    - Add `task_type_counts` to stats responses where applicable
    - _Requirements: 3.7_

- [x] 8. Checkpoint — API compilation and endpoint verification
  - Ensure API project compiles, all controllers wire correctly, and FluentValidation rules are in place. Ask the user if questions arise.

- [x] 9. Frontend — Feature visibility map and template-aware labels
  - [x] 9.1 Create `templateFeatureConfig.ts` with feature visibility map
    - Create `apps/web/lib/utils/templateFeatureConfig.ts`
    - Define `GroupTemplateType` type and `FeatureVisibility` interface
    - Implement `FEATURE_VISIBILITY_MAP` constant with correct visibility flags per template type
    - Ensure Custom template has all flags `true`
    - Include `stayoverLabel` with correct `en`/`he` values per template
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 8.1, 8.2, 8.3, 8.4, 8.6_

  - [x] 9.2 Remove `min_rest_hours` constraints from template seed data in `groupTemplates.ts`
    - Remove all `{ ruleType: "min_rest_hours", ... }` entries from every template's `constraints` array
    - _Requirements: 6.1, 6.2_

  - [x] 9.3 Update `ConstraintsTab.tsx` to use `max_task_type_per_period`
    - Rename any UI references from `max_kitchen_per_week` to `max_task_type_per_period`
    - Update constraint creation form to accept `task_type_name`, `max`, and `period_days` fields
    - Allow admin to select any task type defined in the group
    - _Requirements: 4.1, 4.3_

  - [x] 9.4 Apply feature visibility map to group settings UI
    - Import `FEATURE_VISIBILITY_MAP` in relevant settings components
    - Use the group's `templateType` (from API response) to look up visibility
    - Conditionally render Closed Base toggle, Home Leave config, min rest, min people at base based on visibility flags
    - Use `stayoverLabel` for the closed-base toggle label text
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 9.5 Add template type selector to group creation flow
    - Update group creation UI to show template selector with name, nameHe, description, icon, color
    - Pass selected `templateType` to the create group API call
    - _Requirements: 7.1, 7.4_

  - [ ]* 9.6 Write property test: Feature visibility map completeness (fast-check)
    - **Property 1: Feature visibility map completeness and correctness**
    - For any valid GroupTemplateType, verify the map returns a FeatureVisibility object with correct flags per spec
    - Verify Custom has all booleans true, Army/Security have stayoverLabel.en === "Closed Base", Restaurant/Hospital/Custom have stayoverLabel.en === "Stayover"
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 8.1, 8.2, 8.3, 8.4, 8.6**

- [x] 10. Checkpoint — Frontend verification
  - Ensure frontend compiles (`next build` or `tsc --noEmit`), template seed data has no `min_rest_hours`, and feature visibility map is correctly typed. Ask the user if questions arise.

- [ ] 11. Integration wiring and final cleanup
  - [x] 11.1 Wire template type through group creation flow end-to-end
    - Ensure `CreateGroupCommand` handler reads `templateType` and calls `Group.Create(... templateType)`
    - Ensure `UpdateGroupCommand` handler calls `group.SetTemplateType(...)` when templateType is provided
    - Ensure group API response includes `templateType` field from the entity
    - _Requirements: 1.1, 1.4, 1.5, 7.2_

  - [x] 11.2 Update next-intl messages for template-aware labels
    - Add stayover/closed-base label keys to `en.json` and `he.json` message files
    - Use dynamic label lookup based on template type in relevant components
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [ ]* 11.3 Write property test: Constraint migration preserves semantics (FsCheck)
    - **Property 3: Constraint migration preserves semantics**
    - For any `max_kitchen_per_week` constraint with payload `{"max": N}`, verify migration produces `max_task_type_per_period` with `task_type_name="kitchen"`, `max=N`, `period_days=7`
    - **Validates: Requirements 4.4**

  - [ ]* 11.4 Write property test: Generic task-type counting correctness (FsCheck)
    - **Property 4: Generic task-type counting correctness**
    - For any set of published assignments with task type names, verify CumulativeTracker produces a task_type_counts dictionary where each key's value equals the exact count of assignments with that name, and sum of all values equals total assignments
    - **Validates: Requirements 5.3, 5.5**

  - [ ]* 11.5 Write property test: Template seeding correctness (FsCheck)
    - **Property 5: Template seeding correctness**
    - For any template type (Army, Restaurant, Hospital, Security), verify group creation seeds exactly the defined tasks, qualifications, unavailability reasons, and solver horizon. For Custom, verify zero tasks, zero constraints, zero qualifications, zero unavailability reasons
    - **Validates: Requirements 7.2, 7.3**

- [x] 12. Final checkpoint — Full integration verification
  - Ensure all projects compile, no dead code references remain (grep for DislikedHatedScore, KitchenCount, IsKitchenTask, max_kitchen_per_week in source excluding migrations/docs), solver tests pass, and frontend builds cleanly. Ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- fast-check is used for TypeScript frontend property tests (feature visibility map)
- Hypothesis is used for Python solver property tests (constraint enforcement)
- FsCheck is used for C# backend property tests (migration semantics, task-type counting, template seeding)
- The migration is designed to be idempotent and non-destructive (IF EXISTS guards, safe UPDATE WHERE)
- Existing groups default to `Custom` template type — no functionality lost
- The `min_rest_hours` constraint removal from templates is safe because `MinRestBetweenShiftsHours` on the Group entity already handles this

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3"] },
    { "id": 3, "tasks": ["3.4", "5.1"] },
    { "id": 4, "tasks": ["5.2", "5.3"] },
    { "id": 5, "tasks": ["5.4", "7.1", "7.2", "7.3"] },
    { "id": 6, "tasks": ["9.1", "9.2", "9.3"] },
    { "id": 7, "tasks": ["9.4", "9.5", "9.6"] },
    { "id": 8, "tasks": ["11.1", "11.2"] },
    { "id": 9, "tasks": ["11.3", "11.4", "11.5"] }
  ]
}
```
