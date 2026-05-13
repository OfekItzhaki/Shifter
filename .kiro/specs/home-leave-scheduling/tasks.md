# Implementation Plan: Home-Leave Scheduling

## Overview

This plan implements home-leave scheduling for closed-base army groups across 6 phases: database schema, API backend, Python solver, integration services, frontend UI, and property-based testing. Each phase builds incrementally on the previous, with checkpoints to validate progress. The implementation uses C# (.NET) for the API, Python for the solver, and TypeScript (Next.js) for the frontend.

## Tasks

- [x] 1. Database & Domain Layer
  - [x] 1.1 Create database migration `042_home_leave.sql`
    - Add `is_closed_base BOOLEAN NOT NULL DEFAULT FALSE` column to `groups` table
    - Create `home_leave_configs` table with all columns (id, group_id, space_id, min_rest_hours, eligibility_threshold_hours, leave_capacity, leave_duration_hours, created_at, updated_at)
    - Create `home_leave_templates` table with all columns (id, space_id, name, min_rest_hours, eligibility_threshold_hours, leave_capacity, leave_duration_hours, created_at)
    - Add indexes: `idx_home_leave_configs_group_id`, `idx_home_leave_configs_space_id`, `idx_home_leave_templates_space_name` (unique), `idx_home_leave_templates_space_id`
    - Enable RLS on both tables with `space_id = current_setting('app.current_space_id', TRUE)::UUID` policies
    - Add `updated_at` trigger on `home_leave_configs`
    - File: `infra/migrations/042_home_leave.sql`
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.6, 12.7_

  - [x] 1.2 Create `HomeLeaveConfig` domain entity
    - Create `Domain/Groups/HomeLeaveConfig.cs` implementing `AuditableEntity` and `ITenantScoped`
    - Properties: SpaceId, GroupId, MinRestHours, EligibilityThresholdHours, LeaveCapacity, LeaveDurationHours
    - Include factory method and private setters following existing domain entity patterns
    - _Requirements: 12.2, 12.5_

  - [x] 1.3 Create `HomeLeaveTemplate` domain entity
    - Create `Domain/Groups/HomeLeaveTemplate.cs` implementing `Entity` and `ITenantScoped`
    - Properties: SpaceId, Name, MinRestHours, EligibilityThresholdHours, LeaveCapacity, LeaveDurationHours
    - Include factory method and name validation (1–100 chars, no leading/trailing whitespace)
    - _Requirements: 12.3, 12.5, 10.8_

  - [x] 1.4 Add `IsClosedBase` property to `Group` entity
    - Modify `Domain/Groups/Group.cs` to add `IsClosedBase` boolean property (default false)
    - Add `SetClosedBase(bool value)` method that calls `Touch()`
    - _Requirements: 1.4, 12.1_

  - [x] 1.5 Add EF Core configurations for new entities
    - Create `Infrastructure/Persistence/Configurations/HomeLeaveConfigConfiguration.cs` with Fluent API mapping
    - Create `Infrastructure/Persistence/Configurations/HomeLeaveTemplateConfiguration.cs` with Fluent API mapping
    - Update `AppDbContextConfigurations.cs` to register new DbSets and apply configurations
    - Configure the unique constraint on `home_leave_configs.group_id` and the composite unique index on `home_leave_templates(space_id, name)`
    - _Requirements: 12.2, 12.3, 12.7_

- [x] 2. Checkpoint — Verify database & domain layer
  - Ensure migration runs cleanly against a local database
  - Ensure the API project compiles with new entities and EF configurations
  - Ask the user if questions arise

- [x] 3. API Backend — Home-Leave Config
  - [x] 3.1 Create `UpsertHomeLeaveConfigCommand` and validator
    - Create `Application/HomeLeave/Commands/UpsertHomeLeaveConfigCommand.cs` with MediatR handler
    - Create `Application/HomeLeave/Validators/UpsertHomeLeaveConfigValidator.cs` using FluentValidation
    - Validate: min_rest_hours (4–16), eligibility_threshold_hours (min_rest_hours–48), leave_capacity (1 to group_member_count-1), leave_duration_hours (12–168)
    - Handler: check permission via `IPermissionService`, upsert config record, return updated config
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [x] 3.2 Create `GetHomeLeaveConfigQuery`
    - Create `Application/HomeLeave/Queries/GetHomeLeaveConfigQuery.cs` with MediatR handler
    - Return saved config or default values (min_rest: 8, eligibility_threshold: 24, capacity: 1, duration: 48) if no record exists
    - _Requirements: 2.9_

  - [x] 3.3 Create `HomeLeaveConfigController`
    - Create `Controllers/HomeLeaveConfigController.cs` with routes:
      - `PUT /spaces/{spaceId}/groups/{groupId}/home-leave-config` → dispatches `UpsertHomeLeaveConfigCommand`
      - `GET /spaces/{spaceId}/groups/{groupId}/home-leave-config` → dispatches `GetHomeLeaveConfigQuery`
    - Add `[Authorize]` attribute, permission checks via `IPermissionService.RequirePermissionAsync`
    - Validate group exists and belongs to space (404 if not)
    - _Requirements: 2.2, 2.8, 1.5_

- [x] 4. API Backend — Home-Leave Templates
  - [x] 4.1 Create template CRUD commands and queries
    - Create `Application/HomeLeave/Commands/CreateHomeLeaveTemplateCommand.cs` — validates name (1–100 chars, trimmed, no leading/trailing whitespace), checks duplicate name (409), persists template
    - Create `Application/HomeLeave/Commands/DeleteHomeLeaveTemplateCommand.cs` — deletes template, returns 404 if not found
    - Create `Application/HomeLeave/Queries/ListHomeLeaveTemplatesQuery.cs` — lists templates for space, sorted by created_at descending
    - Create `Application/HomeLeave/Queries/LoadHomeLeaveTemplateQuery.cs` — returns single template by ID, 404 if not found
    - Create validators for each command using FluentValidation
    - _Requirements: 10.2, 10.3, 10.4, 10.5, 10.7, 10.8, 10.9, 10.10_

  - [x] 4.2 Create `HomeLeaveTemplatesController`
    - Create `Controllers/HomeLeaveTemplatesController.cs` with routes:
      - `POST /spaces/{spaceId}/home-leave-templates` → create template
      - `GET /spaces/{spaceId}/home-leave-templates` → list templates
      - `GET /spaces/{spaceId}/home-leave-templates/{templateId}` → load single template
      - `DELETE /spaces/{spaceId}/home-leave-templates/{templateId}` → delete template
    - Add `[Authorize]`, permission checks (`constraints.manage`)
    - _Requirements: 10.2, 10.4, 10.6, 10.9, 10.10_

  - [x] 4.3 Update `GroupsController` to support `isClosedBase` field
    - Add `isClosedBase` to the update group request/response DTOs
    - Ensure `PUT /spaces/{spaceId}/groups/{groupId}` accepts and persists the `isClosedBase` flag
    - Permission check: `constraints.manage` on the group's space
    - _Requirements: 1.2, 1.3, 1.4, 1.5_

- [x] 5. API Backend — Solver Payload & Output Extensions
  - [x] 5.1 Extend `SolverInputDto` with `HomeLeaveConfigDto`
    - Add `HomeLeaveConfigDto` record to `Application/Scheduling/Models/SolverInputDto.cs`
    - Fields: Enabled (bool), MinRestHours (double), EligibilityThresholdHours (double), LeaveCapacity (int), LeaveDurationHours (double)
    - Add optional `HomeLeaveConfig` property to `SolverInputDto`
    - _Requirements: 7.1_

  - [x] 5.2 Extend `SolverOutputDto` with home-leave fields
    - Add `HomeLeaveAssignmentDto` class (PersonId, StartsAt, EndsAt) to `Application/Scheduling/Models/SolverOutputDto.cs`
    - Add `HomeLeaveMetricDto` class (PersonId, TotalBaseHours, TotalHomeHours, BaseTimeRatio, LeaveSlotCount)
    - Add `HomeLeaveAssignments`, `HomeLeaveMetrics`, and `FairnessVariance` properties to `SolverOutputDto`
    - Use `[JsonPropertyName]` attributes with snake_case naming
    - _Requirements: 8.1, 8.2_

  - [x] 5.3 Extend `SolverPayloadNormalizer` to include home-leave config
    - Modify `Infrastructure/Scheduling/SolverPayloadNormalizer.cs` `BuildAsync` method
    - After loading group data: check `group.IsClosedBase == true`
    - Load `HomeLeaveConfig` for the group from DB
    - If config exists with all fields populated → include `HomeLeaveConfigDto(Enabled: true, ...)` in payload
    - If config missing or incomplete → log warning with group ID, omit field
    - _Requirements: 7.3, 7.4, 7.5_

- [x] 6. Checkpoint — Verify API layer compiles and endpoints are wired
  - Ensure the API project compiles cleanly
  - Ensure all new controllers are registered and routes are accessible
  - Ask the user if questions arise

- [x] 7. Solver — Input/Output Model Extensions
  - [x] 7.1 Extend solver input model with `HomeLeaveConfig`
    - Modify `apps/solver/models/solver_input.py`
    - Add `HomeLeaveConfig` Pydantic model: enabled (bool), min_rest_hours (float), eligibility_threshold_hours (float), leave_capacity (int), leave_duration_hours (float)
    - Add optional `home_leave_config: Optional[HomeLeaveConfig] = None` field to `SolverInput`
    - _Requirements: 7.1, 7.2_

  - [x] 7.2 Extend solver output model with home-leave fields
    - Modify `apps/solver/models/solver_output.py`
    - Add `HomeLeaveAssignment` model: person_id (str), starts_at (str), ends_at (str)
    - Add `HomeLeaveMetric` model: person_id (str), total_base_hours (float), total_home_hours (float), base_time_ratio (float), leave_slot_count (int)
    - Add fields to `SolverOutput`: home_leave_assignments (list), home_leave_metrics (list), fairness_variance (Optional[float])
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 8. Solver — Home-Leave Constraint Module
  - [x] 8.1 Create `apps/solver/solver/home_leave.py` — constraint functions
    - Implement `add_home_leave_constraints()`: creates boolean decision variables for home-leave slots (one per person per possible start hour), enforces capacity constraint (at most `leave_capacity` people on leave per hour), enforces no-overlap with mission assignments, enforces min-rest gate before leave starts, enforces one-at-a-time per person
    - Implement `add_home_leave_eligibility_preference()`: soft preference that once a person exceeds eligibility_threshold_hours of continuous free_in_base time, prefer sending them home
    - _Requirements: 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.8_

  - [x] 8.2 Create fairness objective in `home_leave.py`
    - Implement `add_home_leave_fairness_objective()`: minimizes max deviation of base_time_ratio from group mean, weight = 500 (below coverage at 1000, above burden at ≤99)
    - Skip fairness if fewer than 2 eligible members
    - _Requirements: 6.3, 6.4, 6.6_

  - [x] 8.3 Integrate home-leave module into `engine.py`
    - Modify `apps/solver/solver/engine.py`
    - Check if `input.home_leave_config` is present and `enabled == True`
    - Generate home-leave time slots (every hour boundary within horizon, duration = leave_duration_hours)
    - Create boolean decision variables: `home_leave[(person_idx, slot_start_hour)]`
    - Call `add_home_leave_constraints()`, `add_home_leave_eligibility_preference()`, `add_home_leave_fairness_objective()`
    - Add fairness penalties to the objective function
    - Extract results: build `HomeLeaveAssignment` and `HomeLeaveMetric` objects from solved model
    - Calculate `fairness_variance` from base_time_ratios
    - When config absent or disabled: return empty lists and null variance
    - _Requirements: 5.1, 5.4, 5.5, 5.6, 5.7, 6.1, 6.2, 6.5, 6.7, 7.2, 8.3_

  - [x] 8.4 Add min-rest hard constraint for closed-base groups
    - Ensure the existing min-rest constraint logic in `constraints.py` or the new `home_leave.py` enforces the hard constraint from `home_leave_config.min_rest_hours`
    - Return `HardConflict` entries when infeasible due to min-rest violations (include rule_type, affected_person_ids, affected_slot_ids)
    - Respect emergency bypass: skip enforcement for bypassed persons
    - _Requirements: 3.2, 3.3, 3.4, 3.5_

- [x] 9. Checkpoint — Verify solver module
  - Ensure solver starts and accepts payloads with the new `home_leave_config` field
  - Ensure payloads without `home_leave_config` still produce identical output to before
  - Ask the user if questions arise

- [x] 10. Integration — Publish Service & Presence Windows
  - [x] 10.1 Extend publish service to handle home-leave assignments
    - Modify the schedule publish logic (in `Infrastructure/Scheduling/` or `Application/Scheduling/Commands/`)
    - When publishing a schedule version with `home_leave_assignments`:
      - Create synthetic `TaskSlot` records with `task_type = "home_leave"` (or reuse a well-known task type)
      - Create `Assignment` records linking person → synthetic slot → schedule version with `source = "solver"`
      - Create `PresenceWindow` records with `state = "at_home"`, `is_derived = true`, matching person_id, starts_at, ends_at, space_id
    - Validate no overlap with existing `on_mission` presence windows — reject publish with 409 if conflict found
    - Discard entries with unknown person_id or invalid time ranges (starts_at >= ends_at), log warnings
    - _Requirements: 8.4, 8.5, 11.1, 11.2, 11.5_

  - [x] 10.2 Implement home-leave cancellation logic
    - When a group admin cancels a home-leave assignment (schedule override):
      - If `starts_at` is in the future: delete the `at_home` presence window entirely
      - If `starts_at` is in the past and `ends_at` is in the future: truncate the presence window to end at current timestamp
      - Set person's presence state to `free_in_base` for the remaining duration
    - Require `schedule.publish` permission
    - _Requirements: 11.4, 11.5, 11.6_

  - [x] 10.3 Include published `at_home` windows in solver payload
    - Modify `SolverPayloadNormalizer.BuildAsync` to include all published `at_home` presence windows that overlap with the schedule horizon (ends_at > horizon_start AND starts_at < horizon_end)
    - These windows prevent the solver from assigning missions during those periods
    - _Requirements: 11.3_

- [x] 11. Checkpoint — Verify integration layer
  - Ensure publish flow creates presence windows correctly
  - Ensure cancellation truncates/deletes windows as expected
  - Ask the user if questions arise

- [x] 12. Frontend — Group Settings & Configuration
  - [x] 12.1 Add "בסיס סגור" toggle to group settings
    - Add a toggle switch component in the group settings page reflecting `isClosedBase` value
    - On toggle: call `PUT /spaces/{spaceId}/groups/{groupId}` with updated `isClosedBase` field
    - Default: off for new groups
    - _Requirements: 1.1, 1.2_

  - [x] 12.2 Create "הגדרות חופשות" (Leave Settings) panel
    - Conditionally render panel when `isClosedBase = true`, hide when false
    - Form fields: min rest hours (default 8), eligibility threshold hours (default 24), leave capacity (default 1), leave duration hours (default 48)
    - On submit: call `PUT /spaces/{spaceId}/groups/{groupId}/home-leave-config`
    - Show inline validation errors on 400 responses, permission error toast on 403
    - Populate with defaults when no saved config exists
    - _Requirements: 1.6, 1.7, 2.1, 2.2, 2.9_

  - [x] 12.3 Create template save/load UI
    - Add "שמור כתבנית" button that prompts for name and calls `POST /spaces/{spaceId}/home-leave-templates`
    - Add "טען תבנית" dropdown listing templates (sorted by created_at desc) via `GET /spaces/{spaceId}/home-leave-templates`
    - On template select: populate form fields without auto-saving
    - Handle 409 (duplicate name) with error message "שם התבנית כבר קיים"
    - _Requirements: 10.1, 10.2, 10.4, 10.5_

- [ ] 13. Frontend — Schedule Visualization
  - [x] 13.1 Create `HomeLeaveMetricsPanel` component
    - Create `apps/web/components/home-leave/HomeLeaveMetricsPanel.tsx`
    - Display "זמן בבסיס / בבית" panel with per-person stats: name, total base hours (rounded integer), total home hours (rounded integer), base-time ratio as percentage (1 decimal), leave slot count
    - Sort people alphabetically by name
    - Include horizontal stacked bar chart per person (base-time color vs. home-time color)
    - Show fairness warning when max - min base_time_ratio > 15 percentage points
    - Hide panel entirely when no metrics data available
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.6_

  - [x] 13.2 Render home-leave slots on schedule timeline
    - Display home-leave assignments on the Gantt/timeline view with distinct visual style (different color, "בבית" label)
    - Integrate with existing schedule version rendering
    - _Requirements: 9.5_

- [x] 14. Checkpoint — Verify frontend components render correctly
  - Ensure toggle, config panel, template UI, and metrics panel render without errors
  - Ask the user if questions arise

- [ ] 15. Testing — Property-Based Tests (Solver)
  - [ ]* 15.1 Write property test for min-rest invariant (Python/Hypothesis)
    - **Property 3: Minimum rest invariant — no assignment pair violates min rest**
    - Generate random HomeLeaveConfig, PersonEligibility lists (2–8 people), TaskSlot lists (3–20 slots) within 3–7 day horizon
    - Run solver and verify: for every person, no two consecutive mission assignments have gap < min_rest_hours
    - **Validates: Requirements 3.2, 3.3**
    - File: `apps/solver/tests/test_home_leave_properties.py`

  - [ ]* 15.2 Write property test for capacity invariant (Python/Hypothesis)
    - **Property 4: Home-leave capacity invariant**
    - For every hour in the horizon, verify number of people on leave ≤ leave_capacity
    - **Validates: Requirements 4.5, 5.2**
    - File: `apps/solver/tests/test_home_leave_properties.py`

  - [ ]* 15.3 Write property test for leave duration correctness (Python/Hypothesis)
    - **Property 5: Home-leave duration correctness**
    - For every home-leave assignment, verify (ends_at - starts_at) == leave_duration_hours
    - **Validates: Requirements 5.1**
    - File: `apps/solver/tests/test_home_leave_properties.py`

  - [ ]* 15.4 Write property test for no leave-mission overlap (Python/Hypothesis)
    - **Property 6: No overlap between home-leave and mission assignments**
    - For every person, verify no mission assignment overlaps any home-leave assignment
    - **Validates: Requirements 5.3**
    - File: `apps/solver/tests/test_home_leave_properties.py`

  - [ ]* 15.5 Write property test for no concurrent leave per person (Python/Hypothesis)
    - **Property 7: No concurrent home-leave for the same person**
    - For every person, verify no two home-leave assignments overlap in time
    - **Validates: Requirements 5.8**
    - File: `apps/solver/tests/test_home_leave_properties.py`

  - [ ]* 15.6 Write property test for base-time ratio computation (Python/Hypothesis)
    - **Property 8: Base-time ratio computation correctness**
    - For every person in metrics, verify base_time_ratio == total_base_hours / (total_base_hours + total_home_hours) rounded to 4 decimal places
    - **Validates: Requirements 6.1, 6.2**
    - File: `apps/solver/tests/test_home_leave_properties.py`

  - [ ]* 15.7 Write property test for disabled config produces empty output (Python/Hypothesis)
    - **Property 9: Disabled home-leave config produces empty output**
    - When home_leave_config is absent or enabled=false, verify empty lists and null variance
    - **Validates: Requirements 7.2, 8.3**
    - File: `apps/solver/tests/test_home_leave_properties.py`

- [ ] 16. Testing — Property-Based Tests (.NET & Frontend)
  - [ ]* 16.1 Write property test for config validation (.NET/FsCheck)
    - **Property 1: Home-leave config validation accepts valid inputs and rejects invalid inputs**
    - Generate random tuples of (min_rest_hours, eligibility_threshold_hours, leave_capacity, leave_duration_hours, group_member_count)
    - Verify validation accepts iff all range constraints are satisfied
    - **Validates: Requirements 2.4, 2.5, 2.6, 2.7**
    - File: `apps/api/Jobuler.Tests/HomeLeave/HomeLeaveConfigValidationPropertyTests.cs`

  - [ ]* 16.2 Write property test for template name validation (.NET/FsCheck)
    - **Property 11: Template name validation**
    - Generate random strings, verify acceptance iff trimmed length 1–100 and no leading/trailing whitespace
    - **Validates: Requirements 10.8**
    - File: `apps/api/Jobuler.Tests/HomeLeave/HomeLeaveTemplateNamePropertyTests.cs`

  - [ ]* 16.3 Write property test for fairness warning threshold (TypeScript/fast-check)
    - **Property 10: Fairness warning threshold**
    - Generate random arrays of base_time_ratio values (0.0–1.0), verify warning shown iff max - min > 0.15
    - **Validates: Requirements 9.4**
    - File: `apps/web/__tests__/home-leave/fairnessWarning.property.test.ts`

- [x] 17. Final Checkpoint — Full integration verification
  - Ensure all tests pass (unit, property-based, integration)
  - Ensure the API compiles and solver starts without errors
  - Ask the user if questions arise

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation between phases
- Property tests validate universal correctness properties from the design document
- The solver module (`home_leave.py`) is isolated from existing `constraints.py` to avoid regressions
- Frontend tasks assume the existing Next.js component patterns and API client generation via NSwag
- All API endpoints require `[Authorize]` and permission checks per security rules
- RLS policies provide defense-in-depth but application-layer space_id filtering is still required

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "1.4"] },
    { "id": 2, "tasks": ["1.5", "7.1", "7.2"] },
    { "id": 3, "tasks": ["3.1", "3.2", "4.1", "5.1", "5.2"] },
    { "id": 4, "tasks": ["3.3", "4.2", "4.3", "5.3", "8.1"] },
    { "id": 5, "tasks": ["8.2", "8.4"] },
    { "id": 6, "tasks": ["8.3"] },
    { "id": 7, "tasks": ["10.1", "10.3"] },
    { "id": 8, "tasks": ["10.2"] },
    { "id": 9, "tasks": ["12.1", "13.1"] },
    { "id": 10, "tasks": ["12.2", "12.3", "13.2"] },
    { "id": 11, "tasks": ["15.1", "15.2", "15.3", "15.4", "15.5", "15.6", "15.7"] },
    { "id": 12, "tasks": ["16.1", "16.2", "16.3"] }
  ]
}
```
