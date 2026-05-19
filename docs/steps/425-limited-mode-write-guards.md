# 425 — Limited_Mode Write Guards

## Phase

Subscription Cancellation & Renewal — Limited_Mode Enforcement

## Purpose

When a group's subscription expires, the group enters Limited_Mode (`Group.IsActive == false`). This step adds guards to write operations (solver runs, schedule publishing, assignment creation) so that deactivated groups cannot create new schedules, assignments, or trigger solver runs. Read-only operations remain unaffected.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Domain/Groups/Group.cs` | Added `EnsureActive()` method — throws `InvalidOperationException` if `IsActive == false` |
| `Jobuler.Application/Scheduling/Commands/TriggerSolverCommand.cs` | Added Limited_Mode guard after RLS setup — loads group by `GroupId` and calls `EnsureActive()` |
| `Jobuler.Application/Scheduling/Commands/PublishSandboxCommand.cs` | Added `group.EnsureActive()` call after loading the group |
| `Jobuler.Application/Scheduling/Commands/ApplyManualOverrideCommand.cs` | Added Limited_Mode guard — resolves group via `GroupTask` lookup from `SlotId`, then calls `EnsureActive()` |
| `Jobuler.Api/Controllers/SimulationController.cs` | Narrowed `InvalidOperationException` catch in `PublishSandbox` to only version-status errors, allowing Limited_Mode exceptions to propagate as 400 |

## Key decisions

- **Guard on `Group` entity** — `EnsureActive()` is a domain-level method, keeping the business rule encapsulated in the entity rather than scattered across handlers.
- **Guard in command handlers** — Per architecture rules, business logic lives in the Application layer. Each handler loads the group and calls `EnsureActive()` before proceeding.
- **Narrowed exception catch in SimulationController** — The existing `catch (InvalidOperationException)` was too broad and would convert Limited_Mode errors to 409 Conflict. Now it only catches version-status conflicts using `when` clause.
- **Simulate endpoint not guarded** — The `/simulate` endpoint is a stateless read-like operation (no DB writes, no ScheduleRun created). It doesn't "create" a solver run per the requirement definition.
- **ApplyManualOverrideCommand resolves group via GroupTask** — Since the command only has a `SlotId`, the group is resolved by looking up the `GroupTask` that matches the slot ID.

## How it connects

- Depends on: Task 1.1 (Group.Deactivate/Reactivate), Task 3.3 (ExpireSubscriptionsCommand sets `Group.IsActive = false`)
- Used by: Frontend will receive 400 errors with a clear message when attempting write operations on limited groups
- `ExceptionHandlingMiddleware` maps `InvalidOperationException` → 400 Bad Request with the message body

## How to run / verify

```bash
cd apps/api
dotnet build
dotnet test --filter "TriggerSolver|PublishSandbox|ManualOverride|LimitedMode"
```

All tests pass. The guard can be manually verified by:
1. Deactivating a group (`group.Deactivate()`)
2. Attempting to trigger a solver run for that group → 400 "This group is in limited mode..."
3. Attempting to publish a sandbox for that group → 400
4. Attempting a manual override on a slot belonging to that group → 400

## What comes next

- Task 7.2: Property test verifying Limited_Mode blocks write operations
- Task 7.3: Integration tests for the full cancel → expire → renew lifecycle

## Git commit

```bash
git add -A && git commit -m "feat(billing): add Limited_Mode guards to write operations"
```
