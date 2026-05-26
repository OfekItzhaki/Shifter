# 571 — PostgreSQL Advisory Lock Service

## Phase

Self-Service Scheduling — Infrastructure Layer

## Purpose

Implements the `ISlotLockService` interface using PostgreSQL transaction-scoped advisory locks. This provides exclusive locking on shift slots during request processing, preventing two members from claiming the last available spot simultaneously. The lock is automatically released when the enclosing transaction commits or rolls back, ensuring no lock leakage.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Scheduling/PostgresAdvisoryLockService.cs` | Implementation of `ISlotLockService` using `pg_advisory_xact_lock(hashtext(slot_id))` with a 5-second timeout via linked CancellationToken |

## Key decisions

1. **`pg_advisory_xact_lock` over `pg_try_advisory_lock`**: The blocking variant is used because it integrates cleanly with CancellationToken-based timeout. The token cancels the SQL statement if the lock isn't acquired within the timeout window.
2. **`hashtext()` for lock key**: Converts the GUID string to a stable int4 hash suitable for advisory lock keys, matching the design document's specification.
3. **Linked CancellationToken pattern**: A timeout-specific CTS is linked with the caller's CTS. If the timeout fires (but the caller didn't cancel), the method returns `false`. If the caller cancels, the OperationCanceledException propagates normally.
4. **Transaction-scoped lock**: No explicit release is needed — PostgreSQL automatically releases `pg_advisory_xact_lock` on transaction commit or rollback, satisfying requirements 11.4 and 11.5.

## How it connects

- Implements `ISlotLockService` defined in `Jobuler.Application/Scheduling/SelfService/ISlotLockService.cs`
- Will be registered in DI (task 18.1) and consumed by `ShiftRequestService` (task 7.3) to serialize concurrent slot claims
- Follows the same `AppDbContext` + `ExecuteSqlRawAsync` pattern used by `SubmitFeedbackCommandHandler` for advisory locks

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
```

The service requires an active PostgreSQL transaction to function correctly. Integration testing requires a real database connection (advisory locks are not supported by the in-memory provider).

## What comes next

- Task 18.1: Register `PostgresAdvisoryLockService` as `ISlotLockService` in the DI container
- Task 7.3: `ShiftRequestService` will call `TryAcquireSlotLockAsync` before reading slot capacity

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement PostgresAdvisoryLockService for slot locking"
```
