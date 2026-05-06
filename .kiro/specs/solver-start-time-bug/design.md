# Solver Start Time Bug — Bugfix Design

## Overview

The solver generates schedules starting from the wrong point in time when triggered by the auto-scheduler. The root cause is two-fold: (1) `AutoSchedulerService.CheckGroupAsync` calls `TriggerSolverCommand` with `StartTime = null` and no `GroupId`, so the solver always defaults to `DateTime.UtcNow`; and (2) the `Group` entity has no `SolverStartDateTime` field, so there is no way to configure a custom start date per group.

The fix adds a nullable `SolverStartDateTime` (`DateTime?`) to the `Group` domain entity, exposes it through the settings API and frontend UI, and wires `AutoSchedulerService` to pass the configured value as `StartTime` when triggering the solver. The `SolverPayloadNormalizer` already handles a non-null `startTime` correctly — no changes are needed there.

---

## Glossary

- **Bug_Condition (C)**: The condition that triggers the bug — the auto-scheduler triggers a solver run for a group that has a configured `SolverStartDateTime`, but passes `StartTime = null`, causing the solver to use `DateTime.UtcNow` instead of the configured value.
- **Property (P)**: The desired behavior when the bug condition holds — the solver SHALL use `group.SolverStartDateTime` (if set) or `DateTime.UtcNow` (if null) as the horizon start datetime.
- **Preservation**: Existing behavior that must remain unchanged — manual solver triggers from the UI, groups with no `SolverStartDateTime`, horizon length calculation, and all data-filtering logic in `SolverPayloadNormalizer`.
- **`AutoSchedulerService`**: Background service in `Jobuler.Infrastructure/Scheduling/AutoSchedulerService.cs` that periodically checks each group and triggers the solver when coverage gaps are detected.
- **`TriggerSolverCommand`**: MediatR command in `Jobuler.Application/Scheduling/Commands/TriggerSolverCommand.cs` that creates a `ScheduleRun` and enqueues a `SolverJobMessage`. Accepts optional `GroupId` and `StartTime` parameters.
- **`SolverPayloadNormalizer`**: Service in `Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` that builds the full solver input payload. When `startTime` is non-null it uses that value as `nowUtc` and `horizonStartDt`; when null it defaults to `DateTime.UtcNow`.
- **`horizonStartDt`**: The `DateTime` used as the lower bound for filtering availability windows, presence windows, task slots, and for clamping `GroupTask` shift expansion. This is the value that must equal the configured start time.
- **`windowStart`**: The effective start of shift generation for a `GroupTask` — currently `task.StartsAt < horizonStartDt ? horizonStartDt : task.StartsAt`. When `horizonStartDt` is wrong (too early or too late), shifts are generated from the wrong point.
- **`SolverStartDateTime`**: The new nullable `DateTime?` field to be added to the `Group` entity. When set, the auto-scheduler passes it as `StartTime` to `TriggerSolverCommand`.

---

## Bug Details

### Bug Condition

The bug manifests when the auto-scheduler triggers a solver run for a group. `CheckGroupAsync` calls `TriggerSolverCommand` with `StartTime = null` (and no `GroupId`), so `SolverPayloadNormalizer.BuildAsync` receives `startTime = null` and sets `horizonStartDt = DateTime.UtcNow`. The `GroupTask` shift expansion loop then uses `var windowStart = task.StartsAt < horizonStartDt ? horizonStartDt : task.StartsAt` — so if `task.StartsAt` is in the future (e.g. July 7), shifts start from July 7, not from now. There is also no field on `Group` to store a configured start date, so even if the code were fixed to read it, the value would not exist.

**Formal Specification:**
```
FUNCTION isBugCondition(group, triggerSource)
  INPUT:
    group         — a Group entity
    triggerSource — "auto" | "manual"
  OUTPUT: boolean

  RETURN triggerSource = "auto"
         AND group.SolverStartDateTime IS NULL   // field doesn't exist yet
         AND EXISTS task IN group.GroupTasks
               WHERE task.StartsAt > DateTime.UtcNow
                     // shifts start from task.StartsAt, not from UtcNow
END FUNCTION
```

After the fix, the condition that triggers incorrect behavior becomes:

```
FUNCTION isBugCondition_fixed(group, triggerSource)
  // Bug is fixed: auto-scheduler passes group.SolverStartDateTime as StartTime.
  // This function always returns false after the fix is applied.
  RETURN false
END FUNCTION
```

### Examples

- **Example 1 — Future task, no configured start**: Group has a `GroupTask` with `StartsAt = 2025-07-07T00:00Z`. Auto-scheduler triggers solver with `StartTime = null`. `horizonStartDt = DateTime.UtcNow` (e.g. `2025-06-30T10:00Z`). `windowStart = task.StartsAt = 2025-07-07T00:00Z` because `task.StartsAt > horizonStartDt`. Shifts are generated from July 7, not from now. **Expected**: shifts start from `DateTime.UtcNow` (June 30).
- **Example 2 — Configured start date ignored**: Admin sets `SolverStartDateTime = 2025-07-01T06:00Z` on the group (once the field exists). Auto-scheduler triggers solver with `StartTime = null` (current bug). Solver ignores the configured value and uses `DateTime.UtcNow`. **Expected**: solver uses `2025-07-01T06:00Z`.
- **Example 3 — Manual trigger (no bug)**: Admin clicks "Run Schedule" in the UI with `solverStartTime = "2025-07-01T06:00"`. Frontend sends `startTime = "2025-07-01T06:00:00Z"`. `TriggerSolverCommand` receives `StartTime = 2025-07-01T06:00Z`. Solver uses that value correctly. This path is unaffected by the fix.
- **Edge case — Null `SolverStartDateTime` after fix**: Group has no `SolverStartDateTime` set. Auto-scheduler passes `StartTime = null`. Solver defaults to `DateTime.UtcNow`. Behavior is identical to today — no regression.

---

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Manual solver triggers from the settings tab UI must continue to use the `datetime-local` input value as `StartTime`, overriding any group-level `SolverStartDateTime` for that specific run.
- Groups with `SolverStartDateTime = null` must continue to use `DateTime.UtcNow` as the horizon start — backward compatibility is preserved.
- `SolverPayloadNormalizer.BuildAsync` filtering logic (`EndsAt >= horizonStartDt`, `StartsAt <= horizonEndDt`) must remain unchanged.
- `SolverHorizonDays` calculation and the horizon end date must remain independent of `SolverStartDateTime`.
- The stale-task guard in `TriggerSolverCommandHandler` must continue to work correctly when `GroupId` is now passed from the auto-scheduler.
- All existing `UpdateGroupSettingsCommand` behavior (saving `SolverHorizonDays`) must be preserved.

**Scope:**
All inputs that do NOT involve the auto-scheduler triggering a group with a configured `SolverStartDateTime` should be completely unaffected by this fix. This includes:
- Manual solver triggers from the UI (any `startTime` value)
- Groups with no `SolverStartDateTime` configured
- All data filtering in `SolverPayloadNormalizer` (availability, presence, task slots, constraints)
- Fairness counters, baseline assignments, locked slots

---

## Hypothesized Root Cause

Based on the bug description and code review, the causes are:

1. **Missing `SolverStartDateTime` field on `Group`**: The `Group` entity has no field to store a configured solver start date. `UpdateSettings` only accepts `solverHorizonDays`. There is no DB column, no EF mapping, no API surface, and no UI input for this value.

2. **`AutoSchedulerService` passes `StartTime = null`**: In `CheckGroupAsync`, the `TriggerSolverCommand` is constructed as `new TriggerSolverCommand(spaceId, "standard", SystemUserId)` — `GroupId` and `StartTime` are both omitted (defaulting to `null`). Even after the field is added to `Group`, the service must be updated to read and pass it.

3. **`AutoSchedulerService` passes no `GroupId`**: The command is triggered without `GroupId`, so `SolverPayloadNormalizer` builds a space-wide payload instead of a group-scoped one. This is a secondary issue: the stale-task guard and group-scoped member/task filtering in the normalizer are bypassed. The fix should also pass `GroupId`.

4. **`GroupTask` shift expansion uses `task.StartsAt` as fallback**: The line `var windowStart = task.StartsAt < horizonStartDt ? horizonStartDt : task.StartsAt` is correct in isolation — it clamps to `horizonStartDt` when the task has already started. But when `horizonStartDt` is wrong (because `StartTime = null` was passed), the clamping produces the wrong result. This is a symptom, not the root cause.

---

## Correctness Properties

Property 1: Bug Condition — Auto-Scheduler Uses Configured Start Time

_For any_ group where `SolverStartDateTime` is set (non-null) and the auto-scheduler determines a new schedule is needed, the fixed `AutoSchedulerService` SHALL pass `group.SolverStartDateTime` as `StartTime` in `TriggerSolverCommand`, causing `SolverPayloadNormalizer` to use that value as `horizonStartDt` and generate shifts starting from the configured date and time.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation — Null `SolverStartDateTime` Falls Back to `DateTime.UtcNow`

_For any_ group where `SolverStartDateTime` is null and the auto-scheduler triggers a solver run, the fixed `AutoSchedulerService` SHALL pass `StartTime = null`, and the solver SHALL produce the same result as before the fix — using `DateTime.UtcNow` as the horizon start.

**Validates: Requirements 3.1, 3.4**

Property 3: Preservation — Manual Trigger Is Unaffected

_For any_ manual solver trigger from the UI that provides an explicit `startTime` value, the fixed code SHALL produce exactly the same behavior as the original code — using the UI-provided value as `StartTime`, regardless of the group's `SolverStartDateTime`.

**Validates: Requirements 3.2, 3.3**

---

## Fix Implementation

### Changes Required

**1. Domain — `Group` entity**

**File**: `apps/api/Jobuler.Domain/Groups/Group.cs`

**Specific Changes**:
- Add `public DateTime? SolverStartDateTime { get; private set; }` property.
- Update `UpdateSettings` to accept an optional `DateTime? solverStartDateTime` parameter and assign it: `SolverStartDateTime = solverStartDateTime;`

```
// Before
public void UpdateSettings(int solverHorizonDays) { ... }

// After
public void UpdateSettings(int solverHorizonDays, DateTime? solverStartDateTime = null)
{
    SolverHorizonDays = Math.Clamp(solverHorizonDays, 1, 90);
    SolverStartDateTime = solverStartDateTime;
    Touch();
}
```

---

**2. Infrastructure — EF Configuration**

**File**: `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs`

**Specific Changes**:
- Add EF property mapping for `SolverStartDateTime`:
  ```
  builder.Property(g => g.SolverStartDateTime)
      .HasColumnName("solver_start_date_time")
      .IsRequired(false);
  ```

---

**3. Infrastructure — Database Migration**

**File**: new migration in `apps/api/Jobuler.Infrastructure/Migrations/`

**Specific Changes**:
- Add nullable column `solver_start_date_time TIMESTAMPTZ` to the `groups` table.
- No default value — existing rows will have `NULL`, which preserves backward compatibility (falls back to `DateTime.UtcNow`).

---

**4. Application — `UpdateGroupSettingsCommand`**

**File**: `apps/api/Jobuler.Application/Groups/Commands/UpdateGroupSettingsCommand.cs`

**Specific Changes**:
- Add `DateTime? SolverStartDateTime` to the command record.
- Pass it to `group.UpdateSettings(req.SolverHorizonDays, req.SolverStartDateTime)`.

```
// Before
public record UpdateGroupSettingsCommand(Guid SpaceId, Guid GroupId, int SolverHorizonDays) : IRequest;

// After
public record UpdateGroupSettingsCommand(
    Guid SpaceId, Guid GroupId,
    int SolverHorizonDays,
    DateTime? SolverStartDateTime = null) : IRequest;
```

---

**5. API — Controller and Request Record**

**File**: `apps/api/Jobuler.Api/Controllers/GroupsController.cs`

**Specific Changes**:
- Add `DateTime? SolverStartDateTime` to `UpdateGroupSettingsRequest`.
- Pass it through to `UpdateGroupSettingsCommand`.

```
// Before
public record UpdateGroupSettingsRequest(int SolverHorizonDays);

// After
public record UpdateGroupSettingsRequest(int SolverHorizonDays, DateTime? SolverStartDateTime = null);
```

In the `UpdateSettings` action:
```
await _mediator.Send(new UpdateGroupSettingsCommand(
    spaceId, groupId, req.SolverHorizonDays, req.SolverStartDateTime), ct);
```

---

**6. Application — `GetGroupsQuery` DTO**

**File**: `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs`

**Specific Changes**:
- Add `DateTime? SolverStartDateTime` to the `GroupDto` record so the frontend can read the current value and pre-populate the input.

---

**7. Infrastructure — `AutoSchedulerService`**

**File**: `apps/api/Jobuler.Infrastructure/Scheduling/AutoSchedulerService.cs`

**Specific Changes**:
- In `CheckAndTriggerAsync`, include `SolverStartDateTime` in the group projection:
  ```
  .Select(g => new { g.Id, g.SpaceId, g.Name, g.SolverHorizonDays, g.SolverStartDateTime })
  ```
- Pass `SolverStartDateTime` and `GroupId` to `CheckGroupAsync`.
- In `CheckGroupAsync`, pass both to `TriggerSolverCommand`:
  ```
  // Before
  new TriggerSolverCommand(spaceId, "standard", SystemUserId)

  // After
  new TriggerSolverCommand(spaceId, "standard", SystemUserId,
      GroupId: groupId,
      StartTime: group.SolverStartDateTime)
  ```

---

**8. Frontend — API Client**

**File**: `apps/web/lib/api/groups.ts`

**Specific Changes**:
- Add `solverStartDateTime?: string | null` to `GroupWithMemberCountDto`.
- Update `updateGroupSettings` signature to accept `solverStartDateTime?: string | null` and include it in the PATCH body.

```typescript
// Before
export async function updateGroupSettings(
  spaceId: string, groupId: string, solverHorizonDays: number
): Promise<void>

// After
export async function updateGroupSettings(
  spaceId: string, groupId: string,
  solverHorizonDays: number,
  solverStartDateTime?: string | null
): Promise<void>
```

---

**9. Frontend — `SettingsTab.tsx` UI**

**File**: `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx`

**Specific Changes**:
- Add `solverStartDateTime: string | null` and `onSolverStartDateTimeChange: (v: string | null) => void` to the `Props` interface.
- Add a `datetime-local` input in the "Planning Horizon" section (below the horizon slider, above the Save button) so the admin can configure the auto-scheduler start date.
- The input should be optional/clearable — an empty value means "use `DateTime.UtcNow`".
- Wire `onSaveSettings` to pass the new value through to `updateGroupSettings`.

---

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis.

**Test Plan**: Write unit tests that invoke `AutoSchedulerService.CheckGroupAsync` (or the equivalent logic) with a group that has a future `GroupTask.StartsAt`, then assert that the `TriggerSolverCommand` received `StartTime = null` and that the resulting `horizonStartDt` in the normalizer equals `DateTime.UtcNow` rather than the task's start date. Run these tests on the UNFIXED code to observe the failure.

**Test Cases**:
1. **Auto-trigger with future task**: Create a group with `GroupTask.StartsAt = UtcNow + 7 days`. Invoke auto-scheduler. Assert that `TriggerSolverCommand.StartTime` is null and that the solver payload's first shift starts from `UtcNow`, not from `StartsAt`. (Will fail on unfixed code — shifts start from `StartsAt`.)
2. **Auto-trigger with configured `SolverStartDateTime`**: Create a group with `SolverStartDateTime = UtcNow + 1 day`. Invoke auto-scheduler. Assert that `TriggerSolverCommand.StartTime = SolverStartDateTime`. (Will fail on unfixed code — field doesn't exist.)
3. **`GroupId` not passed**: Assert that `TriggerSolverCommand.GroupId` equals the group's ID when triggered by the auto-scheduler. (Will fail on unfixed code — `GroupId` is null.)

**Expected Counterexamples**:
- `TriggerSolverCommand.StartTime` is null even when a `SolverStartDateTime` is configured.
- Shifts in the solver payload start from `task.StartsAt` rather than `DateTime.UtcNow`.
- `TriggerSolverCommand.GroupId` is null, bypassing group-scoped filtering.

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL group WHERE group.SolverStartDateTime IS NOT NULL DO
  command := autoScheduler.BuildTriggerCommand(group)
  ASSERT command.StartTime = group.SolverStartDateTime
  ASSERT command.GroupId   = group.Id

  payload := normalizer.BuildAsync(spaceId, runId, "standard",
                 baselineId, command.GroupId, command.StartTime)
  ASSERT payload.HorizonStart = DateOnly.FromDateTime(group.SolverStartDateTime)
  FOR ALL shift IN payload.Slots WHERE shift comes from GroupTask DO
    ASSERT shift.StartsAt >= group.SolverStartDateTime
  END FOR
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL group WHERE group.SolverStartDateTime IS NULL DO
  command_original := original_autoScheduler.BuildTriggerCommand(group)
  command_fixed    := fixed_autoScheduler.BuildTriggerCommand(group)
  ASSERT command_original.StartTime = command_fixed.StartTime  // both null
  ASSERT command_fixed.GroupId = group.Id                      // GroupId now set (new behavior, not regression)

  payload_original := normalizer.BuildAsync(..., null, null)
  payload_fixed    := normalizer.BuildAsync(..., group.Id, null)
  // horizonStartDt is DateTime.UtcNow in both cases
  ASSERT payload_fixed.HorizonStart = payload_original.HorizonStart
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because it generates many group configurations automatically and verifies that the null-`SolverStartDateTime` path is unchanged across all of them.

**Test Cases**:
1. **Null `SolverStartDateTime` preservation**: For any group with `SolverStartDateTime = null`, assert that the auto-scheduler passes `StartTime = null` and the solver payload's `horizonStart` equals `DateOnly.FromDateTime(DateTime.UtcNow)`.
2. **Manual trigger preservation**: For any manual trigger with an explicit `startTime`, assert that the payload's `horizonStart` equals `DateOnly.FromDateTime(startTime)` — unchanged by the fix.
3. **`SolverHorizonDays` independence**: For any group, assert that changing `SolverStartDateTime` does not affect `SolverHorizonDays` or the horizon end date calculation.
4. **Settings save round-trip**: Assert that `PATCH /spaces/{spaceId}/groups/{groupId}/settings` with `{ solverHorizonDays: 7, solverStartDateTime: "2025-07-01T06:00:00Z" }` persists both values and returns them in the next `GET /spaces/{spaceId}/groups` response.

### Unit Tests

- Test `Group.UpdateSettings` with and without `solverStartDateTime` — verify the field is set correctly and `SolverHorizonDays` is clamped independently.
- Test `UpdateGroupSettingsCommandHandler` — verify it calls `group.UpdateSettings` with both parameters.
- Test `AutoSchedulerService` (with mocked `IMediator`) — verify `TriggerSolverCommand` receives the correct `GroupId` and `StartTime` for groups with and without `SolverStartDateTime`.
- Test `SolverPayloadNormalizer.BuildAsync` with a non-null `startTime` — verify `horizonStartDt` equals the provided value (existing behavior, regression guard).

### Property-Based Tests

- Generate random `DateTime?` values for `SolverStartDateTime` and verify that `AutoSchedulerService` always passes the exact value (or null) as `StartTime` — no truncation, no timezone conversion.
- Generate random groups (some with `SolverStartDateTime`, some without) and verify that the `horizonStart` in the solver payload is always consistent with the `StartTime` passed to the normalizer.
- Generate random `solverHorizonDays` values (1–90) alongside random `SolverStartDateTime` values and verify that the horizon end date is always `horizonStart + solverHorizonDays - 1` days, independent of the start time.

### Integration Tests

- Full auto-scheduler flow: seed a group with `SolverStartDateTime = UtcNow + 2 days`, run `CheckAndTriggerAsync`, assert the enqueued `SolverJobMessage.StartTime` equals the configured value.
- Settings API round-trip: `PATCH` settings with a `solverStartDateTime`, then `GET` groups and assert the value is returned in the DTO.
- Null fallback: seed a group with `SolverStartDateTime = null`, run auto-scheduler, assert `SolverJobMessage.StartTime` is null and the solver payload uses `DateTime.UtcNow`.
