# 568 — SelfServiceConfig Domain Entity

## Phase
Self-Service Scheduling — Domain Layer (Task 1.2)

## Purpose
Provides group-level configuration for self-service scheduling, defining min/max shift constraints, request window offsets, cancellation cutoff, waitlist offer duration, and cycle length. This entity is the single source of truth for all configurable parameters that govern how self-service scheduling behaves for a group.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Groups/SelfServiceConfig.cs` | Domain entity with all config properties, factory methods, validation, and update logic |
| `apps/api/Jobuler.Tests/Domain/SelfServiceConfigTests.cs` | Unit tests covering creation, validation, and update scenarios |

## Key decisions

- **Placed in `Groups/` folder** — follows the same pattern as `HomeLeaveConfig` since it's a group-level configuration entity (one-to-one with Group).
- **Private constructor + static factory** — matches existing entity conventions (`HomeLeaveConfig.Create(...)`, `Group.Create(...)`).
- **Domain-level validation** — all setters validate ranges and throw `InvalidOperationException` on invalid input, consistent with other domain entities.
- **Two `Create` overloads** — one with defaults only (for new groups) and one with all parameters (for explicit configuration).
- **Validation ranges** — Min ∈ [0,100], Max ∈ [1,100], offsets ∈ [1,720]h, waitlist ∈ [15,1440]min, cycle ∈ [1,30] days, open offset > close offset.

## How it connects

- Referenced by `ShiftRequestService` to enforce Max_Shifts and request window timing
- Referenced by `SlotGenerationService` to determine cycle duration
- Referenced by `WaitlistService` for offer timeout duration
- Referenced by `CheckUnderScheduledMembersCommand` for Min_Shifts threshold
- Will be configured via `SelfServiceConfigController` (task 14.1)
- EF Core configuration will be added in task 2.1

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~SelfServiceConfigTests"
```

All 26 tests should pass.

## What comes next

- Task 1.3: `SchedulingCycle` entity
- Task 2.1: EF Core entity configuration for `SelfServiceConfig`
- Task 4.3: Application-layer CRUD commands with FluentValidation

## Git commit

```bash
git add -A && git commit -m "feat(self-service): add SelfServiceConfig domain entity with validation and tests"
```
