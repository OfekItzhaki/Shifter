# 516 — TriggerRegenerationCommand Handler

## Phase

Phase: Schedule Regeneration — Application Layer

## Purpose

Implements the `TriggerRegenerationCommandHandler` which orchestrates the full regeneration trigger flow: RLS setup, subscription validation, concurrency guard with stale run recovery, published version lookup, schedule run creation, and solver job dispatch. Also introduces `PaymentRequiredException` for 402 responses.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Commands/TriggerRegenerationCommand.cs` | Added `TriggerRegenerationCommandHandler` with full handler logic (steps 1–8 from design) |
| `apps/api/Jobuler.Application/Common/PaymentRequiredException.cs` | New exception class for 402 Payment Required responses when subscription is expired |
| `apps/api/Jobuler.Api/Middleware/ExceptionHandlingMiddleware.cs` | Added `PaymentRequiredException` → 402 mapping before the `ConflictException` case |

## Key decisions

- **Subscription check in handler (not controller)**: The design explicitly places the subscription check in the command handler for regeneration, unlike the existing `TriggerSolverCommand` which checks at the controller level. This keeps the regeneration logic self-contained and testable.
- **PaymentRequiredException as a dedicated exception**: Created a new exception type rather than reusing `InvalidOperationException` so the middleware can map it to HTTP 402 specifically.
- **Stale run grace period configurable**: Reads `Solver:StaleGracePeriodMinutes` from configuration (defaults to 5 minutes). Combined with `Solver:TimeoutSeconds` (defaults to 30s), runs older than timeout + grace are marked failed.
- **Space timezone resolution via owner user**: Since the `Space` entity doesn't store timezone directly, the handler resolves "today in space timezone" by looking up the space owner's `CountryCode`/`StateCode` and using `ITimezoneResolver`. Falls back to Asia/Jerusalem.
- **Stale runs marked failed in-place**: When a running regeneration run exceeds the timeout + grace period, it's marked as failed within the same transaction, allowing the new request to proceed.

## How it connects

- **Upstream**: The API controller (task 5.1) will dispatch this command after permission checks.
- **Downstream**: The `SolverJobMessage` is picked up by the background worker (task 7.1) which builds the solver payload and creates the draft version.
- **Dependencies**: Uses `ISolverJobQueue` for job dispatch, `ITimezoneResolver` for space timezone, `AppDbContext` for all DB access, `IConfiguration` for timeout settings.
- **Exception handling**: `PaymentRequiredException` → 402, `ConflictException` → 409, `InvalidOperationException` → 400 (all via `ExceptionHandlingMiddleware`).

## How to run / verify

```bash
cd apps/api
dotnet build
dotnet test --filter "FullyQualifiedName~Regeneration"
```

The handler is auto-discovered by MediatR's assembly scanning. The `PaymentRequiredException` mapping is verified by the API project build.

## What comes next

- Task 4.3–4.5: Property-based tests for concurrent regeneration rejection, subscription gating, and stale run timeout recovery.
- Task 5.1: API endpoint that dispatches this command.

## Git commit

```bash
git add -A && git commit -m "feat(scheduling): implement TriggerRegenerationCommandHandler with subscription, concurrency, and stale run guards"
```
