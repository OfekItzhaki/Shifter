# 292 — Publish Sandbox Command

## Phase

Feature — Draft Simulation Sandbox

## Purpose

Implements the `PublishSandboxCommand` handler that persists all sandbox overrides (tasks, constraints, member exclusions, settings) in a single atomic transaction before delegating to the existing `PublishVersionCommand` for the actual version publish. This is the backend write path that makes sandbox changes permanent when an admin decides to publish.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Commands/PublishSandboxCommand.cs` | MediatR command + handler that orchestrates the full sandbox publish flow |

### Key behaviors implemented:

- **Permission check** — requires `schedule.publish` permission via `IPermissionService`
- **Version validation** — verifies the version exists, belongs to the space, and is in Draft status (returns 409-equivalent for already-published)
- **Task overrides** — add (creates new `GroupTask`), edit (updates existing), remove (deactivates)
- **Constraint overrides** — add (creates new `ConstraintRule`), edit (updates payload/severity), remove (deactivates)
- **Member exclusions** — removes `GroupMembership` records for excluded people
- **Settings overrides** — updates `Group.MinRestBetweenShiftsHours` and `HomeLeaveConfig` parameters
- **Atomic transaction** — wraps all writes in `BeginTransactionAsync` with rollback on failure
- **Delegation** — calls `PublishVersionCommand` within the transaction for the actual publish
- **Audit log** — captures before/after snapshots and logs via `IAuditLogger`
- **RLS compliance** — sets PostgreSQL session variables for tenant isolation

## Key decisions

1. **Explicit transaction** — Unlike most commands that rely on a single `SaveChangesAsync`, this command uses `BeginTransactionAsync` because it performs multiple logical write operations that must all succeed or all fail together, including the delegated `PublishVersionCommand`.

2. **Execution strategy wrapper** — Uses `CreateExecutionStrategy().ExecuteAsync()` to handle PostgreSQL transient failures correctly with the explicit transaction.

3. **Member exclusion via removal** — Excludes members by removing their `GroupMembership` record rather than adding a separate opt-out table. This keeps the data model simple and means excluded members won't appear in future solver runs.

4. **Snapshot-based audit** — Captures task/constraint/member counts before and after the operation for the audit trail, rather than logging individual changes.

5. **Internal snapshot records** — Uses `internal` records for audit snapshots to keep them accessible for testing but not part of the public API surface.

## How it connects

- **Consumed by**: `SimulationController` (task 2.2 will wire the endpoint)
- **Delegates to**: `PublishVersionCommand` for the actual version publish logic
- **Uses**: `AppDbContext`, `IPermissionService`, `IAuditLogger`, `IMediator`
- **DTOs**: `PublishSandboxRequest`, `TaskOverrideDto`, `ConstraintOverrideDto`, `SettingsOverrideDto` (created in task 1.3)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj
dotnet build Jobuler.Api/Jobuler.Api.csproj
```

Both projects compile cleanly with 0 errors.

## What comes next

- Task 2.2: Wire the `POST /spaces/{spaceId}/groups/{groupId}/publish-sandbox` endpoint in `SimulationController` via MediatR
- Task 11.2: Unit tests for transaction behavior and audit log creation

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): implement PublishSandboxCommand handler with atomic transaction"
```
