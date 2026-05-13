# Step 241 — Solver Payload Balance Value

## Phase

Home-Leave Slider — Backend Preview & Solver Payload Integration

## Purpose

Include the stored `balance_value` from `HomeLeaveConfig` in the solver payload DTO so the solver can use it to compute the home-leave eligibility preference weight (`balance_value × 4`).

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Scheduling/Models/SolverInputDto.cs` | Added `BalanceValue` parameter (with `[JsonPropertyName("balance_value")]`) to `HomeLeaveConfigDto` record, defaulting to 50 |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Pass `hlConfig.BalanceValue` when constructing `HomeLeaveConfigDto` in `BuildAsync` |

## Key decisions

- Used a default value of 50 on the DTO parameter for backward compatibility — if any other code constructs the DTO without specifying `BalanceValue`, it defaults to the midpoint (weight = 200).
- Follows the existing `[JsonPropertyName("snake_case")]` pattern used by all other fields in the record.

## How it connects

- The solver Python model already expects `balance_value` in the `home_leave_config` object (added in step 237).
- The weight mapping `balance_value × 4` is implemented in the solver (step 238).
- This step ensures the stored value flows from the database through the normalizer into the solver payload for regular (non-preview) runs.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build should succeed with no errors.

## What comes next

- Task 6.5: Preview controller endpoint
- Task 6.6: Property test verifying payload includes correct `balance_value`

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): include balance_value in solver payload DTO"
```
