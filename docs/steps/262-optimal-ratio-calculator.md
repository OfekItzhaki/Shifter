# Step 262 — Optimal Ratio Calculator Service

## Phase

Home-Leave Overhaul — Application Layer Services

## Purpose

Implements the `OptimalRatioCalculator` service that computes the optimal base:home day ratio for a group. This is the core formula that powers Automatic Mode — it determines how many days at base are needed before a member becomes eligible for home leave, given the group's size, leave capacity, and coverage requirements.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/IOptimalRatioCalculator.cs` | Interface + `OptimalRatioResult` record defining the contract |
| `apps/api/Jobuler.Application/HomeLeave/OptimalRatioCalculator.cs` | Iterative formula implementation that converges in 2-3 iterations |
| `apps/api/Jobuler.Api/Program.cs` | DI registration as singleton (stateless, pure computation) |

## Key decisions

1. **Singleton lifetime** — The calculator has no dependencies on scoped services (no DbContext, no HTTP context). It's pure math, so singleton avoids unnecessary allocations.
2. **Iterative convergence** — The formula is self-referential (`cycle_length = base_days + home_days`, but `base_days` depends on `cycle_length`). We iterate until `base_days` stabilizes, with a safety cap of 20 iterations (typically converges in 2-3).
3. **Input validation in the service** — The calculator validates its inputs and throws `InvalidOperationException` for invalid states, which the `ExceptionHandlingMiddleware` maps to HTTP 400.
4. **IsReduced flag** — Set when `leaveDurationHours < 24`, indicating the group has reduced home-leave availability (less than a full day per visit).

## How it connects

- Called by the `HomeLeaveConfigController` GET endpoint to include optimal ratio in responses
- Called by the `UpsertHomeLeaveConfigCommand` handler when in Automatic Mode
- Called by the preview endpoint for feasibility calculations
- The `FeasibilityEngine` (task 2.2) will complement this by evaluating whether a given ratio is achievable

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
```

Build should succeed with no new warnings.

## What comes next

- Task 2.2: `FeasibilityEngine` service implementation
- Task 2.3: Property-based tests for the optimal ratio formula
- Task 4.3: GET endpoint that exposes the calculated optimal ratio to the frontend

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): implement OptimalRatioCalculator service"
```
