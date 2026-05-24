# Step 533 — UpdateSpaceHomeLeaveConfigCommand

## Phase

Phase — Space Management (Application Layer)

## Purpose

Implements the `UpdateSpaceHomeLeaveConfigCommand` with FluentValidation, allowing the Space Owner to create or update the space-level home-leave configuration. This centralizes leave policy management at the space level rather than per-group.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Spaces/Commands/UpdateSpaceHomeLeaveConfigCommand.cs` | Command record with all home-leave config fields, plus handler that verifies ownership via `IPermissionService`, loads or creates `SpaceHomeLeaveConfig`, updates all fields, and saves |
| `Jobuler.Application/Spaces/Validators/UpdateSpaceHomeLeaveConfigCommandValidator.cs` | FluentValidation validator enforcing: valid enum for Mode, BalanceValue [0,100], BaseDays ≥ 1, HomeDays ≥ 1, MinPeopleAtBase ≥ 1, MinRestHours [0,16], EligibilityThresholdHours [0,9999], LeaveCapacity ≥ 1, LeaveDurationHours [12,168] |

## Key decisions

- **Permission key: `Permissions.OwnershipTransfer`** — matches the pattern used by `UpdateManagementTimeoutCommand` for owner-only operations.
- **Upsert pattern**: If no `SpaceHomeLeaveConfig` exists for the space, creates one via the domain factory method; otherwise updates each field individually via domain setters (preserving domain validation).
- **All fields required on command**: Unlike the group-level `UpsertHomeLeaveConfigCommand` which has optional fields, the space-level command requires all fields since it represents the full configuration state.
- **Validator mirrors domain validation**: FluentValidation catches invalid inputs early in the MediatR pipeline before the handler executes, providing consistent error messages.

## How it connects

- **Upstream**: `SpacesController` (task 11.3) will dispatch this command via MediatR at `PUT /spaces/{spaceId}/home-leave-config`.
- **Domain**: Uses `SpaceHomeLeaveConfig.Create()` factory and individual setters from task 1.3.
- **Pipeline**: `ValidationBehavior` auto-discovers the validator and runs it before the handler.
- **Downstream**: Solver payload normalizer (task 10.1) reads `SpaceHomeLeaveConfig` to override group-level values.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application
dotnet build Jobuler.Api
dotnet build Jobuler.Tests
```

All three projects should compile without errors.

## What comes next

- Task 7.3: `AssignSpaceRoleCommand` and handler
- Task 7.6: Property tests for settings commands (Properties 8, 9, 10, 11)
- Task 11.3: API endpoint wiring in `SpacesController`

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add UpdateSpaceHomeLeaveConfigCommand with FluentValidation"
```
