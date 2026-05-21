# Step 475 — Health Check Monitor Property Tests

## Phase

Phase 4 — Health Check Alerts (Testing)

## Purpose

Validates the HealthCheckMonitorService state machine behavior through property-based tests. These tests verify that state transitions (healthy→unhealthy, unhealthy→healthy) produce the correct side effects (alerts, recovery logs) and that cooldown logic correctly suppresses duplicate alerts while allowing new alerts after recovery.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/HealthChecks/HealthCheckMonitorPropertyTests.cs` | FsCheck property tests for Properties 4, 5, 7, 8 from the design document |
| `apps/api/Jobuler.Infrastructure/HealthChecks/HealthCheckMonitorService.cs` | Changed `ProcessResultsAsync` from `private` to `internal` for testability; fixed state overwrite bug in the state machine |

## Key decisions

1. **Direct state machine testing** — Instead of testing through the full BackgroundService lifecycle (which involves random 5-30s delays and 30s intervals), we test `ProcessResultsAsync` directly. This makes tests fast (~1s total) and deterministic.

2. **Made ProcessResultsAsync internal** — The method was `private` but the `InternalsVisibleTo` attribute was already configured for the test project. Making it `internal` allows direct testing without reflection.

3. **Fixed state overwrite bug** — The original code had a general state update block that ran after the specific transition handlers, overwriting the `LastAlertSentUtc` value they had just set. This caused cooldown to never work. The fix restructures the if/else chain so each branch exclusively handles its own state update.

## How it connects

- Tests validate Properties 4, 5, 7, 8 from the design document
- Validates Requirements 3.4, 3.5, 4.1, 4.3, 4.4, 5.3
- Depends on the HealthCheckMonitorService (task 6.1) and IPushoverNotifier (task 4.1)
- The bug fix ensures cooldown behavior works correctly in production

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~HealthCheckMonitorPropertyTests"
```

All 4 property tests should pass (100 iterations each).

## What comes next

- Task 6.3: Property test for exception resilience (Property 6)
- Task 6.4: Property test for interval clamping (Property 9)

## Git commit

```bash
git add -A && git commit -m "feat(health-checks): add monitor state machine property tests and fix cooldown bug"
```
