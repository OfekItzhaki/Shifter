# Step 531 — UpdateManagementTimeoutCommand

## Phase

Space Management — Application Layer Settings Commands

## Purpose

Implements the command, handler, and FluentValidation validator for updating the space-level management timeout. This allows the Space Owner to configure how long admin sessions last before requiring re-authentication, with the value constrained to [5, 120] minutes.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Spaces/Commands/UpdateManagementTimeoutCommand.cs` | Command record (`SpaceId`, `Minutes`, `UserId`) + MediatR handler that verifies ownership via `IPermissionService`, loads the Space entity, and calls `SetManagementTimeout(minutes)` |
| `apps/api/Jobuler.Application/Spaces/Validators/UpdateManagementTimeoutCommandValidator.cs` | FluentValidation validator ensuring `Minutes` is in [5, 120], and `SpaceId`/`UserId` are non-empty |

## Key decisions

- **Permission key**: Uses `Permissions.OwnershipTransfer` which is an owner-only permission, ensuring only the Space Owner can update the timeout.
- **Dual validation**: FluentValidation catches invalid input before the handler runs; the domain method `SetManagementTimeout` also validates as a safety net.
- **Single-file command + handler**: Follows the established pattern (e.g., `DiscardVersionCommand.cs`) of co-locating the command record and handler in one file.

## How it connects

- The `Space.SetManagementTimeout(int)` domain method (created in task 1.1) enforces the business rule.
- The `IPermissionService` (enhanced in task 3.1) verifies the caller is the Space Owner.
- The API endpoint (task 11.3) will dispatch this command via MediatR.
- The frontend `ManagementTimeoutCard` (task 14.1) will call the API endpoint.

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build
```

Build should succeed with zero errors related to these files.

## What comes next

- Task 7.2: `UpdateSpaceHomeLeaveConfigCommand` and handler
- Task 11.3: API endpoint `PUT /spaces/{spaceId}/management-timeout`
- Task 7.6: Property tests for management timeout validation (Property 8)

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add UpdateManagementTimeoutCommand with FluentValidation"
```
