# 243 — Solver Payload BuildPreviewAsync Method

## Phase
Home-Leave Slider — Infrastructure Layer

## Purpose
Adds a `BuildPreviewAsync` method to `SolverPayloadNormalizer` that builds a solver payload identical to a normal run but overrides the `balance_value` with a provided value and sets `preview_mode = true`. This enables the preview endpoint to call the solver with a user-specified slider value without persisting anything.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Scheduling/Models/SolverInputDto.cs` | Added `PreviewMode` property (`bool`, default `false`, serialized as `preview_mode`) to `SolverInputDto` |
| `apps/api/Jobuler.Application/Scheduling/ISolverPayloadNormalizer.cs` | Added `BuildPreviewAsync(Guid spaceId, Guid groupId, int balanceValue, CancellationToken ct)` to the interface |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Implemented `BuildPreviewAsync` — delegates to `BuildAsync`, then overrides `HomeLeaveConfig.BalanceValue` and sets `PreviewMode = true` |

## Key decisions
- **Reuse `BuildAsync`**: Rather than duplicating the complex payload-building logic, `BuildPreviewAsync` calls `BuildAsync` with a synthetic `runId` and `triggerMode: "preview"`, then applies the overrides via `with` expressions on the immutable records.
- **Synthetic run ID**: Preview runs use `Guid.NewGuid()` since they are ephemeral and never persisted.
- **No baseline**: Preview runs pass `baselineVersionId: null` since stability against a previous version is irrelevant for a quick preview.

## How it connects
- The `PreviewHomeLeaveHandler` (task 6.4) will call `BuildPreviewAsync` to get the payload, then send it to the solver.
- The `PreviewMode` flag on `SolverInputDto` is consumed by the Python solver (task 5.3) to use reduced time limits and worker count.

## How to run / verify
```bash
cd apps/api && dotnet build
```
Build succeeds with no new warnings.

## What comes next
- `PreviewHomeLeaveHandler` implementation that uses `BuildPreviewAsync` and calls the solver client.
- Preview controller endpoint wiring.

## Git commit
```bash
git add -A && git commit -m "feat(home-leave-slider): add BuildPreviewAsync to SolverPayloadNormalizer"
```
