# 264 — Update UpsertHomeLeaveConfigCommand to Support New Mode System

## Phase

Home-Leave Overhaul — API Endpoint Updates (Task 4.1)

## Purpose

Extends the existing `UpsertHomeLeaveConfigCommand` to support the new three-mode system (Automatic, Manual, Emergency Freeze). The handler now orchestrates domain method calls based on the selected mode, computes optimal ratios via `IOptimalRatioCalculator`, and returns feasibility results via `IFeasibilityEngine`.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Commands/UpsertHomeLeaveConfigCommand.cs` | Added `Mode`, `BaseDays`, `HomeDays`, `SliderValue`, `EmergencyFreezeActive`, `EmergencyUseForScheduling` to command record. Updated `HomeLeaveConfigResult` to include mode, days, freeze state, feasibility, and optimal ratio. Handler now injects `IOptimalRatioCalculator` and `IFeasibilityEngine`, calls domain methods based on mode. |
| `apps/api/Jobuler.Application/HomeLeave/Validators/UpsertHomeLeaveConfigValidator.cs` | Extended validator with rules for new fields: Mode enum validation, BaseDays/HomeDays required for Manual mode, SliderValue range check. |

## Key decisions

1. **Backward compatibility** — All new command parameters are optional with `null` defaults. If `Mode` is not provided, defaults to `Automatic`. Existing callers (controller, tests) continue to work without changes.
2. **Coverage requirement formula** — Uses `memberCount - leaveCapacity` as the coverage requirement (minimum people needed at base). This is the simplest correct proxy for MVP.
3. **Optimal ratio computed on write** — When in Automatic mode, the optimal ratio is computed during the upsert and returned in the response, avoiding a separate API call.
4. **Feasibility included in response** — The handler evaluates feasibility after persisting and includes it in the result, giving the frontend immediate feedback.
5. **Emergency freeze via command** — Freeze activation/deactivation is handled within the same upsert command rather than a separate endpoint, keeping the API surface minimal.
6. **Validator relaxed for eligibility threshold** — Upper bound changed from 336 to 9999 to accommodate emergency freeze scenarios where `eligibility_threshold_hours = 9999`.

## How it connects

- **Domain entity** (`HomeLeaveConfig`) — Handler calls `SetMode`, `SetSliderPosition`, `SetRatio`, `ActivateEmergencyFreeze`, `DeactivateEmergencyFreeze` based on request parameters.
- **OptimalRatioCalculator** — Called when mode is Automatic to compute the optimal base:home ratio for the group.
- **FeasibilityEngine** — Called after save to evaluate whether the current configuration satisfies coverage requirements.
- **Controller** (task 4.2) — Will be updated to pass new fields from the request DTO to this command.
- **SolverPayloadNormalizer** (task 6.1) — Reads the persisted mode/days/freeze state to construct solver payloads.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --filter "FullyQualifiedName~HomeLeave"
```

All 98 existing HomeLeave tests pass without modification.

## What comes next

- Task 4.2: Update `HomeLeaveConfigController` PUT endpoint with new request DTO to pass mode fields to this command.
- Task 4.3: Add `GET optimal-ratio` endpoint.
- Task 4.4: Update GET response DTO.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): update UpsertHomeLeaveConfigCommand to support mode system"
```
