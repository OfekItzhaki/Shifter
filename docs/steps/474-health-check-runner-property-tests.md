# Step 474 — Health Check Runner Property Tests

## Phase

Health Check Alerts — Testing

## Purpose

Validates the correctness of the `HealthCheckRunner` aggregation logic using property-based tests. These tests ensure that the runner always produces complete reports, derives overall status correctly, and marks timed-out services as unhealthy — regardless of the combination of service results.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/HealthChecks/HealthCheckRunnerPropertyTests.cs` | Property-based tests covering Properties 1, 2, and 3 from the design document |

### Properties tested

1. **Response structure completeness** — For any set of service health check results, the report contains an entry for every registered service, a UTC timestamp, and the version string (Requirements 1.1, 1.4)
2. **Overall status derivation** — For any set of results, overall status is "healthy" iff all non-skipped services report "healthy"; otherwise "degraded" (Requirements 1.2, 1.3)
3. **Timeout marks service unhealthy** — For any check exceeding 10s timeout, the resulting status is "unhealthy" (Requirements 2.6)

## Key decisions

- Used FsCheck.Xunit `[Property(MaxTest = 100)]` for properties 1 and 2, and `[Fact]` for property 3 (timeout behavior requires real async delays, not suitable for pure property generation)
- Tested `DeriveOverallStatus` both indirectly (through `RunAllAsync`) and directly (internal static method accessible via InternalsVisibleTo)
- Used NSubstitute to mock `IServiceHealthCheck` instances with generated results
- Created custom FsCheck generators for unique service name/status combinations

## How it connects

- Tests validate the `HealthCheckRunner` implemented in step 468
- Uses the interfaces defined in step 465
- Part of the health-check-alerts spec task 3.2

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~Jobuler.Tests.HealthChecks.HealthCheckRunnerPropertyTests"
```

All 6 tests should pass (2 property tests + 1 direct DeriveOverallStatus property test + 3 timeout fact tests).

## What comes next

- Task 4.2: PushoverNotifier unit tests
- Task 6.2: HealthCheckMonitorService state machine property tests (Properties 4, 5, 7, 8)

## Git commit

```bash
git add -A && git commit -m "feat(health-checks): add HealthCheckRunner property tests (Properties 1, 2, 3)"
```
