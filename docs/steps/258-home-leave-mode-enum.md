# 258 — Home-Leave Mode Enum

## Phase

Home-Leave Overhaul — Domain Layer Foundation

## Purpose

Introduces the `HomeLeaveMode` enum to the Domain layer, enabling the system to distinguish between Automatic and Manual home-leave configuration modes. This is a prerequisite for updating the `HomeLeaveConfig` entity with mode-aware behavior.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Groups/HomeLeaveMode.cs` | New enum with `Automatic` and `Manual` values |

## Key decisions

- Placed in `Jobuler.Domain.Groups` namespace alongside `HomeLeaveConfig`, since the mode is intrinsic to the home-leave configuration entity.
- Followed the existing enum pattern (e.g., `TaskBurdenLevel`) — simple file-scoped namespace, no explicit integer values, no attributes.
- Emergency Freeze is modeled as a separate boolean flag on the entity (not a third enum value), per the design document's decision that mode and freeze are independent.

## How it connects

- Used by `HomeLeaveConfig.Mode` and `HomeLeaveConfig.PreFreezeMode` properties (task 1.4).
- Referenced by the EF Core configuration for string column mapping (task 1.5).
- Used in API request/response DTOs and the `SolverPayloadNormalizer` mode-based logic.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain/Jobuler.Domain.csproj
```

The project should compile without errors.

## What comes next

- Task 1.4: Update `HomeLeaveConfig` domain entity with new fields and methods that use this enum.
- Task 1.5: EF Core configuration to map the `Mode` column as a string.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-overhaul): add HomeLeaveMode enum to Domain layer"
```
