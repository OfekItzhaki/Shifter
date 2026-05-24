# Step 538 — Soft-Delete and Restore API Endpoints

## Phase

Phase — Space Management (API Layer)

## Purpose

Adds the `DELETE /spaces/{spaceId}` and `POST /spaces/{spaceId}/restore` endpoints to `SpacesController`, wiring the existing `SoftDeleteSpaceCommand` and `RestoreSpaceCommand` handlers to HTTP endpoints. This enables clients to soft-delete and restore spaces via the API.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/SpacesController.cs` | Added `SoftDeleteSpace` (DELETE) and `RestoreSpace` (POST /restore) action methods that dispatch the corresponding MediatR commands and return 204 No Content |

## Key decisions

- **No permission checks in controller**: Per architecture rules, permission enforcement happens in the Application layer handlers (`SoftDeleteSpaceCommandHandler` and `RestoreSpaceCommandHandler`), keeping the controller thin.
- **204 No Content response**: Both endpoints return `NoContent()` on success, matching the existing pattern used by `UpdateSpace` and `TransferOwnership`.
- **Class-level `[Authorize]`**: The controller already has `[Authorize]` at the class level, so no additional attribute is needed on the new methods.
- **Route pattern consistency**: Uses `{spaceId:guid}` route constraint matching all other space endpoints.

## How it connects

- **Application layer (tasks 5.1, 5.2)**: Dispatches `SoftDeleteSpaceCommand` and `RestoreSpaceCommand` which handle permission checks, cascade logic, and audit logging.
- **Frontend (task 13.1)**: The API client will call these endpoints via `softDeleteSpace(spaceId)` and `restoreSpace(spaceId)`.
- **DangerZoneCard (task 16.1)**: The delete button in the frontend danger zone section will call `DELETE /spaces/{spaceId}`.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

The project builds successfully with no errors or warnings related to the new endpoints.

## What comes next

- Task 11.2: Add ownership transfer endpoint (already exists, verify wiring)
- Task 11.3: Add settings endpoints (management timeout, home-leave config, invite code)
- Task 11.4: Add role assignment endpoint
- Task 11.5: Update listing queries to exclude soft-deleted spaces

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add soft-delete and restore API endpoints"
```
