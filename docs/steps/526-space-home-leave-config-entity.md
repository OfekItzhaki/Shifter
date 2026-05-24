# Step 526 — SpaceHomeLeaveConfig Entity

## Phase

Space Management — Domain Layer

## Purpose

Creates the space-level home-leave configuration entity that centralizes leave rotation parameters for all closed-base groups within a space. When present, this configuration overrides group-level home-leave settings in the solver payload normalizer, establishing the space as the single source of truth for leave policy.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Spaces/SpaceHomeLeaveConfig.cs` | New entity extending `AuditableEntity` and implementing `ITenantScoped`, with all home-leave configuration properties, a factory `Create` method, individual setter methods for each configurable field, emergency freeze activate/deactivate methods, and domain validation |

## Key decisions

- Follows the same property set and validation rules as the existing `HomeLeaveConfig` in `Jobuler.Domain/Groups/`, ensuring consistency between group-level and space-level configurations.
- References `HomeLeaveMode` enum from `Jobuler.Domain.Groups` namespace rather than duplicating it — the enum is shared across both levels.
- Uses individual `Set*` methods (e.g., `SetMode`, `SetBaseDays`, `SetMinPeopleAtBase`) rather than a single `Update` method, giving command handlers fine-grained control over which fields to update.
- Includes `ActivateEmergencyFreeze` and `DeactivateEmergencyFreeze` methods mirroring the group-level behavior for consistency.
- Private parameterless constructor for EF Core compatibility, with a static `Create` factory method for explicit construction.
- Validation messages are in English (unlike the Hebrew messages in the group-level entity) for consistency with newer code.

## How it connects

- Reuses `HomeLeaveMode` enum from `Jobuler.Domain.Groups` (task 1.3 dependency)
- Will be mapped to `space_home_leave_configs` table via EF Core configuration (task 2.1)
- Consumed by `UpdateSpaceHomeLeaveConfigCommand` handler (task 7.2)
- Read by solver payload normalizer to override group-level values (task 10.1)
- Queried by `GetSpaceHomeLeaveConfigQuery` (task 8.1)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain/Jobuler.Domain.csproj
```

Build succeeds with zero errors.

## What comes next

- Task 1.4: `SpacePermissionLevel` enum (already done — step 525)
- Task 1.5: Enhance `SpaceMembership` with permission level
- Task 2.1: EF Core configuration for `SpaceHomeLeaveConfig`
- Task 7.2: `UpdateSpaceHomeLeaveConfigCommand` handler

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add SpaceHomeLeaveConfig domain entity"
```
