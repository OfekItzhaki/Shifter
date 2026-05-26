# 583 — Self-Service Config Controller

## Phase

Phase: Self-Service Scheduling — API Layer

## Purpose

Exposes the self-service scheduling configuration (min/max shifts, request window offsets, cancellation cutoff, waitlist offer duration, cycle duration) via REST endpoints so that frontend clients can read and update group-level self-service settings.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/SelfServiceConfigController.cs` | New controller with GET and PUT endpoints for self-service config |

## Key decisions

- **Route**: `spaces/{spaceId}/groups/{groupId}/self-service-config` — follows the existing pattern of nesting group-level config under the group route (same as `home-leave-config`).
- **GET permission**: `SpaceView` — any authenticated member who can view the space can read the config. This allows the frontend to display config values to members (e.g., showing request window info).
- **PUT permission**: `ConstraintsManage` — only admins with constraint management permission can update the config. This matches the pattern used by `HomeLeaveConfigController`.
- **Default response**: When no config exists yet, the GET endpoint returns hardcoded defaults (Min=0, Max=7, OpenOffset=168h, CloseOffset=24h, CancellationCutoff=24h, WaitlistOffer=60min, CycleDuration=7d) rather than a 404.
- **FluentValidation**: Validation is wired through the existing `UpdateSelfServiceConfigCommandValidator` (created in task 4.3) which enforces Min ≤ Max, range constraints, and offset ordering.
- **MediatR dispatch**: Controller dispatches `GetSelfServiceConfigQuery` and `UpdateSelfServiceConfigCommand` — no business logic in the controller.

## How it connects

- Depends on `UpdateSelfServiceConfigCommand` and `GetSelfServiceConfigQuery` (task 4.3)
- Depends on `IPermissionService` for authorization checks
- Used by the frontend to configure self-service scheduling settings for a group
- Validates requirements 5.1, 5.2, 5.3 (min/max constraints) and 6.1, 6.2 (request window offsets)

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

The controller is automatically discovered by ASP.NET Core's controller convention. Test with:
- `GET /spaces/{spaceId}/groups/{groupId}/self-service-config` — returns config or defaults
- `PUT /spaces/{spaceId}/groups/{groupId}/self-service-config` — creates/updates config

## What comes next

- Task 14.2: ShiftTemplatesController
- Task 14.3: ShiftSlotsController
- Task 14.4: ShiftRequestsController

## Git commit

```bash
git add -A && git commit -m "feat(self-service): add SelfServiceConfigController with GET/PUT endpoints"
```
