# 573 — Self-Service Config CRUD Commands and Queries

## Phase

Self-Service Scheduling — Application Layer

## Purpose

Implements the `UpdateSelfServiceConfigCommand` and `GetSelfServiceConfigQuery` for managing group-level self-service scheduling configuration. This enables group admins to configure min/max shift constraints, request window offsets, cancellation cutoff, waitlist offer duration, and cycle duration. The command uses FluentValidation to enforce all business rules at the application boundary, and the handler ensures that lowering Max_Shifts does not revoke existing approved shift requests (Requirement 5.7).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/Commands/UpdateSelfServiceConfigCommand.cs` | MediatR command, FluentValidation validator, and handler for creating/updating self-service config |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Queries/GetSelfServiceConfigQuery.cs` | MediatR query and handler for retrieving current config for a group |
| `apps/api/Jobuler.Tests/Validation/UpdateSelfServiceConfigCommandValidatorTests.cs` | 28 unit tests covering all validation rules (boundary values, invalid ranges, required fields) |

### Pre-existing fixes (unrelated to this task)

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Scheduling/ChangeSchedulingModeCommandTests.cs` | Fixed `ThrowsAsync` → `Task.FromException` for NSubstitute 5.x compatibility |

## Key decisions

1. **Upsert pattern**: The `UpdateSelfServiceConfigCommand` handler creates a new config if none exists for the group, or updates the existing one. This avoids requiring a separate "create" command.
2. **Validation at two levels**: FluentValidation catches invalid input at the application boundary (before hitting the domain). The domain entity also has its own guards as a safety net.
3. **Requirement 5.7 compliance**: Lowering `MaxShiftsPerCycle` simply persists the new value without touching existing `Approved` ShiftRequests. The domain `Update` method applies the new config values without side effects on existing data.
4. **Shared DTO**: `SelfServiceConfigDto` is defined alongside the command and reused by the query, keeping the response shape consistent.

## How it connects

- The `SelfServiceConfig` domain entity (task 1.2) provides the business logic and validation guards
- The `AppDbContext.SelfServiceConfigs` DbSet (task 2.1) provides persistence
- The `SelfServiceConfigController` (task 14.1) will wire these commands/queries to HTTP endpoints
- Property tests (task 4.4) will validate Properties 13, 15, and 17 against this implementation

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~UpdateSelfServiceConfigCommandValidatorTests"
```

All 28 tests should pass.

## What comes next

- Task 4.4: Property tests for config validation (Properties 13, 15, 17)
- Task 14.1: `SelfServiceConfigController` wiring GET/PUT endpoints

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement SelfServiceConfig CRUD commands and queries"
```
