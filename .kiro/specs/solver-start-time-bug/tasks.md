# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Auto-Scheduler Ignores Configured SolverStartDateTime
  - **CRITICAL**: This test MUST FAIL on unfixed code — failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior — it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate that `AutoSchedulerService` passes `StartTime = null` and `GroupId = null` even when a group has a configured `SolverStartDateTime`
  - **Scoped PBT Approach**: Scope the property to the concrete failing cases — groups with a non-null `SolverStartDateTime` and at least one future `GroupTask`
  - Test setup: mock `IMediator` to capture the `TriggerSolverCommand` sent by `AutoSchedulerService`; seed a group with `SolverStartDateTime = UtcNow + 1 day` and a `GroupTask` with `StartsAt = UtcNow + 7 days`
  - Assert `TriggerSolverCommand.StartTime == group.SolverStartDateTime` — will FAIL on unfixed code because the field doesn't exist and the command is constructed without `StartTime`
  - Assert `TriggerSolverCommand.GroupId == group.Id` — will FAIL on unfixed code because `GroupId` is omitted from the command
  - For the PBT variant: generate random `DateTime` values for `SolverStartDateTime` (any value in the next 30 days) and verify the command always receives the exact value — no truncation, no timezone shift
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (this is correct — it proves the bug exists)
  - Document counterexamples found: e.g. `TriggerSolverCommand.StartTime` is null, `TriggerSolverCommand.GroupId` is null
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Null SolverStartDateTime Falls Back to DateTime.UtcNow
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: on unfixed code, `AutoSchedulerService` sends `TriggerSolverCommand(spaceId, "standard", SystemUserId)` with no `StartTime` and no `GroupId` for every group regardless of configuration
  - Observe: `SolverPayloadNormalizer.BuildAsync` with `startTime = null` sets `nowUtc = DateTime.UtcNow` and `horizonStartDt = DateTime.UtcNow`
  - Observe: `SolverPayloadNormalizer.BuildAsync` with `startTime = someValue` sets `nowUtc = someValue` and `horizonStartDt = someValue` — this path is already correct and must not regress
  - Write property-based test: for all groups where `SolverStartDateTime` is null, the auto-scheduler passes `StartTime = null` and the solver payload's `horizonStart` equals `DateOnly.FromDateTime(DateTime.UtcNow)` — same as today
  - Write property-based test: for any manual trigger with an explicit `startTime` value, `SolverPayloadNormalizer.BuildAsync` produces `horizonStart = DateOnly.FromDateTime(startTime)` — unchanged by the fix
  - Write property-based test: for any group, changing `SolverStartDateTime` does not affect `SolverHorizonDays` or the horizon end date (`horizonEnd = horizonStart + solverHorizonDays - 1`)
  - Verify all tests PASS on UNFIXED code (confirms baseline behavior to preserve)
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3. Fix: wire SolverStartDateTime through the full stack

  - [x] 3.1 Add `SolverStartDateTime` property to `Group` domain entity
    - Add `public DateTime? SolverStartDateTime { get; private set; }` to `Group.cs`
    - Update `UpdateSettings` signature: `public void UpdateSettings(int solverHorizonDays, DateTime? solverStartDateTime = null)`
    - Assign `SolverStartDateTime = solverStartDateTime;` inside `UpdateSettings` (after the `Math.Clamp` for `SolverHorizonDays`)
    - `SolverHorizonDays` clamping must remain unchanged: `SolverHorizonDays = Math.Clamp(solverHorizonDays, 1, 90);`
    - File: `apps/api/Jobuler.Domain/Groups/Group.cs`
    - _Bug_Condition: isBugCondition — Group has no SolverStartDateTime field, so the value can never be stored or read_
    - _Expected_Behavior: group.SolverStartDateTime stores the configured value and is readable by AutoSchedulerService_
    - _Preservation: SolverHorizonDays clamping and Touch() call are unchanged_
    - _Requirements: 2.1, 2.3_

  - [x] 3.2 Add EF Core mapping for `solver_start_date_time` column
    - Add to `GroupConfiguration.Configure` in `GroupsConfiguration.cs`:
      ```csharp
      builder.Property(g => g.SolverStartDateTime)
          .HasColumnName("solver_start_date_time")
          .IsRequired(false);
      ```
    - File: `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs`
    - _Requirements: 2.3_

  - [x] 3.3 Create database migration for `solver_start_date_time` column
    - Add a new migration file in `apps/api/Jobuler.Infrastructure/Migrations/`
    - Migration adds nullable column: `solver_start_date_time TIMESTAMPTZ NULL` on the `groups` table
    - No default value — existing rows will have `NULL`, preserving backward compatibility (auto-scheduler falls back to `DateTime.UtcNow`)
    - Run `dotnet ef migrations add AddSolverStartDateTimeToGroups` from the Infrastructure project to generate the migration, then verify the generated `Up`/`Down` methods are correct
    - _Requirements: 2.3, 3.1_

  - [x] 3.4 Add `SolverStartDateTime` to `UpdateGroupSettingsCommand`
    - Add `DateTime? SolverStartDateTime = null` parameter to the `UpdateGroupSettingsCommand` record
    - Update the handler to call `group.UpdateSettings(req.SolverHorizonDays, req.SolverStartDateTime)`
    - File: `apps/api/Jobuler.Application/Groups/Commands/UpdateGroupSettingsCommand.cs`
    - _Bug_Condition: UpdateGroupSettingsCommand has no SolverStartDateTime parameter, so the value can never be saved_
    - _Expected_Behavior: handler passes SolverStartDateTime to group.UpdateSettings_
    - _Preservation: SolverHorizonDays is still passed and clamped; existing callers that omit SolverStartDateTime continue to work (default null)_
    - _Requirements: 2.3, 3.4_

  - [x] 3.5 Update `UpdateGroupSettingsRequest` and controller action in `GroupsController`
    - Add `DateTime? SolverStartDateTime = null` to the `UpdateGroupSettingsRequest` record
    - Update the `UpdateSettings` action to pass `req.SolverStartDateTime` through to `UpdateGroupSettingsCommand`:
      ```csharp
      await _mediator.Send(new UpdateGroupSettingsCommand(
          spaceId, groupId, req.SolverHorizonDays, req.SolverStartDateTime), ct);
      ```
    - File: `apps/api/Jobuler.Api/Controllers/GroupsController.cs`
    - _Requirements: 2.3_

  - [x] 3.6 Add `SolverStartDateTime` to `GroupDto` in `GetGroupsQuery`
    - Add `DateTime? SolverStartDateTime` to the `GroupDto` record
    - Update the `Select` projection in `GetGroupsQueryHandler` to include `g.SolverStartDateTime`
    - File: `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs`
    - _Requirements: 2.3_

  - [x] 3.7 Update `AutoSchedulerService` to project and pass `SolverStartDateTime`
    - In `CheckAndTriggerAsync`, update the group projection to include `SolverStartDateTime`:
      ```csharp
      .Select(g => new { g.Id, g.SpaceId, g.Name, g.SolverHorizonDays, g.SolverStartDateTime })
      ```
    - Update `CheckGroupAsync` signature to accept `DateTime? solverStartDateTime`
    - Update the `TriggerSolverCommand` call to pass both `GroupId` and `StartTime`:
      ```csharp
      new TriggerSolverCommand(spaceId, "standard", SystemUserId,
          GroupId: groupId,
          StartTime: solverStartDateTime)
      ```
    - File: `apps/api/Jobuler.Infrastructure/Scheduling/AutoSchedulerService.cs`
    - _Bug_Condition: isBugCondition — AutoSchedulerService calls TriggerSolverCommand with StartTime = null and GroupId = null_
    - _Expected_Behavior: AutoSchedulerService passes group.SolverStartDateTime as StartTime and group.Id as GroupId_
    - _Preservation: groups with SolverStartDateTime = null still pass StartTime = null, preserving DateTime.UtcNow fallback in SolverPayloadNormalizer_
    - _Requirements: 2.1, 2.2, 2.4, 3.1_

  - [x] 3.8 Update frontend API client in `groups.ts`
    - Add `solverStartDateTime?: string | null` to the `GroupWithMemberCountDto` interface
    - Update `updateGroupSettings` signature to accept `solverStartDateTime?: string | null`
    - Include `solverStartDateTime` in the PATCH request body:
      ```typescript
      await apiClient.patch(`/spaces/${spaceId}/groups/${groupId}/settings`, {
        solverHorizonDays,
        solverStartDateTime,
      });
      ```
    - File: `apps/web/lib/api/groups.ts`
    - _Requirements: 2.3_

  - [x] 3.9 Add `datetime-local` input for auto-scheduler start time in `SettingsTab.tsx`
    - Add `solverStartDateTime: string | null` and `onSolverStartDateTimeChange: (v: string | null) => void` to the `Props` interface
    - Add a `datetime-local` input in the "Planning Horizon" section, below the horizon slider and above the Save button:
      - Label: use the `t("solverStartDateTime")` translation key (or a suitable existing key)
      - Value: `solverStartDateTime ?? ""` — empty string means "use `DateTime.UtcNow`" (no configured start)
      - `onChange`: call `onSolverStartDateTimeChange(e.target.value || null)` so clearing the field sets it back to null
    - Update `onSaveSettings` call site (in `page.tsx`) to pass `solverStartDateTime` through to `updateGroupSettings`
    - File: `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` (and the parent `page.tsx` for state + handler wiring)
    - _Requirements: 2.3, 2.5_

  - [x] 3.10 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Auto-Scheduler Uses Configured SolverStartDateTime
    - **IMPORTANT**: Re-run the SAME test from task 1 — do NOT write a new test
    - The test from task 1 encodes the expected behavior: `TriggerSolverCommand.StartTime == group.SolverStartDateTime` and `TriggerSolverCommand.GroupId == group.Id`
    - When this test passes, it confirms the fix is correct
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.11 Verify preservation tests still pass
    - **Property 2: Preservation** - Null SolverStartDateTime Falls Back to DateTime.UtcNow
    - **IMPORTANT**: Re-run the SAME tests from task 2 — do NOT write new tests
    - Run all preservation property tests from step 2
    - **EXPECTED OUTCOME**: All tests PASS (confirms no regressions)
    - Confirm: groups with `SolverStartDateTime = null` still produce `StartTime = null` in the command
    - Confirm: manual triggers with explicit `startTime` are unaffected
    - Confirm: `SolverHorizonDays` and horizon end date are independent of `SolverStartDateTime`

- [x] 4. Checkpoint — Ensure all tests pass
  - Run the full test suite for `Jobuler.Application` and `Jobuler.Infrastructure`
  - Confirm Property 1 (bug condition) passes — auto-scheduler passes `SolverStartDateTime` as `StartTime`
  - Confirm Property 2 (preservation) passes — null fallback and manual trigger paths are unchanged
  - Confirm `Group.UpdateSettings` unit tests pass with and without `solverStartDateTime`
  - Confirm `UpdateGroupSettingsCommandHandler` unit test passes — calls `group.UpdateSettings` with both parameters
  - Confirm the settings API round-trip: `PATCH /spaces/{spaceId}/groups/{groupId}/settings` with `{ solverHorizonDays: 7, solverStartDateTime: "2025-07-01T06:00:00Z" }` persists both values and returns `solverStartDateTime` in the next `GET /spaces/{spaceId}/groups` response
  - Ensure all tests pass; ask the user if questions arise
