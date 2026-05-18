# 348 — Cancel Home Leave Handler Protection

## Phase

Home Leave Protection — Enhanced CancelHomeLeaveCommand

## Purpose

Hardens the `CancelHomeLeaveCommand` handler to block automated invocations and enforce confirmation/permission checks at the handler level (defense-in-depth). This ensures home leave can only be cancelled through explicit admin action, never by automated processes, and that the EmergencyFreeze state is consulted before allowing recalls.

## What was built

- **Modified**: `apps/api/Jobuler.Application/HomeLeave/Commands/CancelHomeLeaveCommand.cs`
  - Added `Guid.Empty` check to block automated invocations (automated services use `Guid.Empty` as the requesting user ID)
  - Added handler-level `Confirmed` check as defense-in-depth (validator also enforces this)
  - Moved permission check after the automated-invocation guard (fail fast)
  - Added EmergencyFreeze state lookup via the person's group membership and HomeLeaveConfig
  - When EmergencyFreeze is NOT active, the handler enforces explicit confirmation (redundant with validator, but protects against validator bypass)

## Key decisions

1. **Automated invocation detection via `Guid.Empty`**: The `AutoSchedulerService` uses `null` (cast to `Guid?`) for system-triggered operations. Since `CancelHomeLeaveCommand.RequestingUserId` is a non-nullable `Guid`, `Guid.Empty` is the sentinel value for automated callers. Rejecting `Guid.Empty` blocks any automated process from cancelling home leave.

2. **Defense-in-depth confirmation check**: The FluentValidation validator already rejects `Confirmed = false`, but the handler adds its own check. This protects against scenarios where the validator pipeline is bypassed (e.g., direct handler invocation in tests or internal code).

3. **EmergencyFreeze lookup via GroupMembership → HomeLeaveConfig**: The handler resolves the person's group via `GroupMemberships`, then loads the `HomeLeaveConfig` to check `EmergencyFreezeActive`. This follows the same pattern used by `SolverPayloadNormalizer`.

4. **Permission check placement**: `RequirePermissionAsync` runs after the automated-invocation guard but before any data mutation, following the security rules.

## How it connects

- Builds on task 4.1 (command record with `Confirmed` parameter) and task 4.2 (FluentValidation validator)
- The handler is called from `HomeLeaveConfigController` (API layer) which already has `[Authorize]`
- Later tasks (5.x, 6.x, 9.x) will add notification dispatch and audit logging after the successful truncation/deletion

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj
```

The handler now:
- Throws `UnauthorizedAccessException` if `RequestingUserId == Guid.Empty`
- Throws `InvalidOperationException` if `Confirmed == false`
- Throws `UnauthorizedAccessException` (via `RequirePermissionAsync`) if user lacks `SchedulePublish` permission
- Loads EmergencyFreeze state and enforces confirmation when not in emergency mode

## What comes next

- Task 4.4: Property test for reason length validation
- Task 4.5: Unit tests for recall command edge cases (confirmation rejection, permission denial, etc.)
- Task 5.x: Recall notification service integration
- Task 6.x: Audit logging integration

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-protection): add confirmation and permission guards to CancelHomeLeaveCommand handler"
```
