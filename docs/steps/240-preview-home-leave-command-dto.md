# 240 — Preview Home Leave Command & DTOs

## Phase
Home-Leave Slider — Backend Preview Endpoint

## Purpose
Define the MediatR command and response DTOs for the home-leave preview feature. These records form the contract between the API controller and the application handler that will orchestrate the preview solver run.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Commands/PreviewHomeLeaveCommand.cs` | Command record (`PreviewHomeLeaveCommand`) and response DTOs (`HomeLeavePreviewResponse`, `CoverageGapDto`) |

## Key decisions

- **Command includes `RequestingUserId`** — consistent with other commands in the project (e.g., `UpsertHomeLeaveConfigCommand`) so the handler can perform permission checks via `IPermissionService`.
- **`Status` is a string** — uses `"optimal" | "feasible" | "no_solution"` to match the solver output directly without an intermediate enum, keeping serialization simple.
- **`CoverageGapDto` uses string timestamps** — `StartsAt`/`EndsAt` are ISO-8601 strings, matching the solver output format and avoiding timezone conversion issues at the DTO layer.
- **`FairnessSpread` is decimal** — provides precision for the ratio difference between highest and lowest `base_time_ratio`.

## How it connects

- **Upstream**: The `HomeLeaveConfigController` (task 6.5) will dispatch this command via MediatR.
- **Downstream**: The `PreviewHomeLeaveHandler` (task 6.2) will implement `IRequestHandler<PreviewHomeLeaveCommand, HomeLeavePreviewResponse>`.
- **Frontend**: The `useHomeLeavePreview` hook (task 8.2) will consume the response shape.

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build succeeds with no new warnings.

## What comes next

- Task 6.2: Implement `PreviewHomeLeaveHandler` that handles this command.
- Task 6.5: Create the controller endpoint that dispatches this command.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add PreviewHomeLeaveCommand and response DTOs"
```
