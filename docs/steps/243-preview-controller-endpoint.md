# 243 — Preview Controller Endpoint

## Phase
Home-Leave Slider — Task 6.5

## Purpose
Expose the `POST /spaces/{spaceId}/groups/{groupId}/home-leave-preview` endpoint so the frontend can request a lightweight preview of the schedule impact when the admin moves the balance slider.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/HomeLeaveConfigController.cs` | Added `IPermissionService` injection, `Preview` action method, and `HomeLeavePreviewRequest` record |

## Key decisions

1. **Permission check at controller level** — The endpoint requires `constraints.manage` permission, checked via `IPermissionService.RequirePermissionAsync` before dispatching the command. This follows the same pattern used in `ConstraintsController` and `HomeLeaveTemplatesController`.
2. **Inline validation for balance_value** — Returns 400 if `BalanceValue` is outside [0, 100], providing a fast-fail before hitting MediatR.
3. **Route override with `~/`** — Uses `[HttpPost("~/spaces/{spaceId:guid}/groups/{groupId:guid}/home-leave-preview")]` to define an absolute route independent of the controller's base route, since the preview URL differs from the config URL.

## How it connects

- Dispatches `PreviewHomeLeaveCommand` (created in task 6.1) via MediatR
- The handler (task 6.2) builds the solver payload and calls the solver synchronously
- Frontend `useHomeLeavePreview` hook (task 8.2) calls this endpoint on slider change

## How to run / verify

```bash
cd apps/api && dotnet build
```

Build succeeds with zero errors.

## What comes next

- Task 6.6: Property test for solver payload balance_value
- Task 6.7: Property test for preview result transformation
- Task 8.2: Frontend `useHomeLeavePreview` hook that calls this endpoint

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-slider): add preview controller endpoint"
```
