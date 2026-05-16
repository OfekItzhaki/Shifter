# 266 — Solver Payload Mode-Based Construction

## Phase

Home-Leave Overhaul — Solver Integration

## Purpose

Updates the `SolverPayloadNormalizer` to construct the solver's `home_leave_config` payload based on the new mode system (Automatic, Manual, Emergency Freeze). Previously, the normalizer simply forwarded stored values. Now it applies mode-specific logic to determine `eligibility_threshold_hours`, `balance_value`, and `min_rest_hours`, and handles emergency freeze states by either zeroing out balance or omitting the config entirely.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Added `BuildHomeLeaveConfigDto` helper method that applies mode/emergency logic; updated `BuildAsync` to use it; updated `BuildPreviewAsync` to handle emergency freeze (rebuilds config from DB when omitted) |

## Key decisions

- **Helper method pattern**: Extracted `BuildHomeLeaveConfigDto` as a static helper that encapsulates all mode/emergency logic in one place, making it testable and reusable.
- **Emergency freeze + no scheduling = null**: When `EmergencyFreezeActive && !EmergencyUseForScheduling`, the method returns `null` which causes `homeLeaveConfigDto` to be null in the payload — the solver then ignores home-leave entirely.
- **Emergency freeze + scheduling = balance 0, threshold 9999**: This effectively prevents anyone from becoming eligible for leave while still including them in task scheduling.
- **Manual mode = neutral balance (50)**: Manual mode uses a fixed balance of 50 since the admin explicitly controls the ratio via `BaseDays`/`HomeDays`.
- **Automatic mode = stored BalanceValue**: The slider position is persisted as `BalanceValue` and forwarded directly.
- **min_rest_hours always 0**: The new day-based system handles rest implicitly via the eligibility threshold.
- **Preview bypasses emergency freeze**: `BuildPreviewAsync` always shows what the schedule would look like with the given parameters, even if emergency freeze is active. If the config was omitted due to freeze, it rebuilds from stored values.

## How it connects

- Depends on `HomeLeaveConfig` entity (task 1.4) which has `Mode`, `BaseDays`, `EmergencyFreezeActive`, `EmergencyUseForScheduling` properties.
- Depends on `HomeLeaveMode` enum (task 1.3) for mode discrimination.
- Used by the solver run pipeline — every solver invocation goes through `BuildAsync`.
- Used by the preview endpoint via `BuildPreviewAsync` → `PreviewHomeLeaveHandler`.
- The solver (`home_leave.py`) receives the same `HomeLeaveConfigDto` shape — no solver changes needed.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --no-build --filter "HomeLeave"
```

Verify manually:
1. A group in Automatic mode produces `eligibility_threshold_hours = BaseDays × 24` and `balance_value` from stored slider.
2. A group in Manual mode produces `eligibility_threshold_hours = BaseDays × 24` and `balance_value = 50`.
3. Emergency freeze + use for scheduling produces `balance_value = 0`, `eligibility_threshold_hours = 9999`.
4. Emergency freeze + don't use for scheduling produces `homeLeaveConfigDto = null` (omitted from payload).
5. Preview endpoint always returns a valid config regardless of freeze state.

## What comes next

- Property tests for ratio-to-solver-params round trip (task 6.3)
- Property tests for emergency freeze solver payload (task 6.4)
- Property tests for min_rest_hours invariant (task 6.5)

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-overhaul): mode-based solver payload construction in SolverPayloadNormalizer"
```
