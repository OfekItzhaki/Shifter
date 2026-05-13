# 245 — Preview Home-Leave Handler

## Phase
Home-Leave Slider Feature — Backend Preview Endpoint

## Purpose
Implements the `PreviewHomeLeaveHandler` that orchestrates the preview solver flow: validates the group, builds a solver payload with the overridden `balance_value` and `preview_mode = true`, calls the solver synchronously with a 5-second timeout, and transforms the solver output into a `HomeLeavePreviewResponse` with counts, coverage gaps, and fairness metrics.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Commands/PreviewHomeLeaveHandler.cs` | MediatR handler implementing the full preview flow |
| `apps/api/Jobuler.Application/Scheduling/Models/SolverOutputDto.cs` | Added `SolverTimeMs` property to capture solver wall-clock time |

## Key decisions

1. **5-second timeout via linked CancellationTokenSource** — Uses `CancellationTokenSource.CreateLinkedTokenSource` to combine the caller's cancellation token with a 5-second deadline, ensuring the preview never blocks longer than necessary.
2. **Graceful degradation on failure** — On timeout, network error, or cancellation, returns `status: "no_solution"` with `solver_time_ms: 0` instead of propagating an exception. This matches the design's requirement that preview never returns 5xx.
3. **Coverage gap calculation via sweep-line algorithm** — Converts home-leave assignments into timeline events (+1 at start, -1 at end), sorts them, and sweeps to find windows where concurrent leaves exceed `leave_capacity`.
4. **Fairness spread** — Computed as `max(base_time_ratio) - min(base_time_ratio)` across all people in the solver's home-leave metrics.
5. **Error messages in Hebrew** — Validation errors use Hebrew strings consistent with the existing codebase pattern.

## How it connects

- Depends on `ISolverPayloadNormalizer.BuildPreviewAsync` (task 6.3) to build the payload
- Depends on `ISolverClient.SolveAsync` to call the solver
- Used by the preview controller endpoint (task 6.5) via MediatR dispatch
- Consumes `PreviewHomeLeaveCommand` and produces `HomeLeavePreviewResponse` (task 6.1)

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build succeeds with no errors related to this handler.

## What comes next

- Task 6.5: Create the preview controller endpoint that dispatches `PreviewHomeLeaveCommand`
- Task 6.6–6.8: Property tests for payload balance_value, result transformation, and coverage gaps

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): implement PreviewHomeLeaveHandler with solver integration"
```
