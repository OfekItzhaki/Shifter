# 186 — HomeLeaveConfig Domain Entity

## Phase

Home-Leave Scheduling — Database & Domain Layer (Task 1.2)

## Purpose

Creates the `HomeLeaveConfig` domain entity that models the home-leave configuration for a closed-base group. This entity holds the four key parameters the solver needs: minimum rest hours, eligibility threshold, leave capacity, and leave duration.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Groups/HomeLeaveConfig.cs` | Domain entity implementing `AuditableEntity` and `ITenantScoped`. Includes a static `Create` factory method, an `Update` method, and private validation helpers for all four configuration fields. |

## Key decisions

- **Private constructor + static factory** — follows the existing pattern in `Group.cs`, `ConstraintRule.cs`, and `PresenceWindow.cs`.
- **Domain-level validation** — basic range checks are enforced in the entity itself (min rest 4–16, threshold ≥ min rest and ≤ 48, capacity ≥ 1, duration 12–168). The upper-bound check for `leave_capacity` against group member count is deferred to the Application layer (FluentValidation) since the entity doesn't have access to group membership data.
- **Decimal types for hours** — matches the database schema (`DECIMAL NOT NULL`) and allows fractional hour values.
- **Update method calls Touch()** — ensures `UpdatedAt` is refreshed on every change, consistent with other auditable entities.

## How it connects

- Referenced by `HomeLeaveConfigConfiguration.cs` (EF Core mapping, task 1.5)
- Used by `UpsertHomeLeaveConfigCommand` (task 3.1) and `GetHomeLeaveConfigQuery` (task 3.2)
- Loaded by `SolverPayloadNormalizer` to build the solver input (task 5.3)
- Maps to the `home_leave_configs` table created in migration `042_home_leave.sql` (task 1.1)

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build --no-restore
```

The project compiles cleanly with zero warnings.

## What comes next

- Task 1.3: `HomeLeaveTemplate` domain entity
- Task 1.5: EF Core configuration for `HomeLeaveConfig`

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add HomeLeaveConfig domain entity"
```
