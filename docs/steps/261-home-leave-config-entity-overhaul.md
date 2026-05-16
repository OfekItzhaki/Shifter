# 261 — Home-Leave Config Domain Entity Overhaul

## Phase

Home-Leave Overhaul — Domain Layer

## Purpose

Update the `HomeLeaveConfig` domain entity to support the new three-mode system (Automatic, Manual, Emergency Freeze). The entity gains new properties for mode tracking, day-based ratios, and emergency freeze state, plus methods for mode switching, ratio setting, slider interpolation, and freeze activation/deactivation.

## What was built

- **`apps/api/Jobuler.Domain/Groups/HomeLeaveConfig.cs`** — Updated with:
  - New properties: `Mode`, `BaseDays`, `HomeDays`, `EmergencyFreezeActive`, `EmergencyUseForScheduling`, `FreezeStartedAt`, `PreFreezeMode`
  - `SetMode(HomeLeaveMode mode)` — switches mode, recalculates solver params (sets `EligibilityThresholdHours = BaseDays * 24`, `MinRestHours = 0`)
  - `SetRatio(int baseDays, int homeDays)` — validates and converts to solver params
  - `SetSliderPosition(int sliderValue, int optimalBaseDays, int optimalHomeDays)` — interpolates between optimal and extremes
  - `ActivateEmergencyFreeze(bool useForScheduling)` — records pre-freeze mode, sets freeze state with timestamp
  - `DeactivateEmergencyFreeze()` — restores pre-freeze mode, clears freeze state
  - Validation: `baseDays >= 1`, `homeDays >= 1`, `leaveDurationHours in [12, 168]`
  - Updated `Create` and `Update` factory/methods with optional new-field parameters (backward-compatible)
  - Relaxed `EligibilityThresholdHours` upper bound from 336 to 9999 (needed for emergency freeze payload)

## Key decisions

1. **Backward-compatible signatures** — New parameters on `Create` and `Update` are optional with sensible defaults, so existing callers don't break.
2. **Slider interpolation** — At position 50 = optimal ratio. Below 50 interpolates toward conservative (14 base, 1 home). Above 50 interpolates toward generous (1 base, 7 home).
3. **Emergency freeze guards** — `ActivateEmergencyFreeze` throws if already active; `DeactivateEmergencyFreeze` throws if not active. This prevents double-activation bugs.
4. **EligibilityThresholdHours upper bound** — Raised to 9999 to support the emergency freeze payload pattern (`eligibility_threshold_hours = 9999` effectively prevents leave eligibility).

## How it connects

- The `HomeLeaveMode` enum (task 1.3) is used as the type for `Mode` and `PreFreezeMode` properties.
- The EF Core configuration (task 1.5) will map these new properties to the database columns added in migration 053 (task 1.1).
- The `UpsertHomeLeaveConfigCommand` (task 4.1) will call `SetMode`, `SetRatio`, `SetSliderPosition`, and freeze methods.
- The `SolverPayloadNormalizer` (task 6.1) reads `Mode`, `BaseDays`, `EmergencyFreezeActive`, etc. to construct the solver payload.

## How to run / verify

```bash
cd apps/api
dotnet build
dotnet test --filter "FullyQualifiedName~HomeLeave"
```

All 81 HomeLeave tests pass. Full solution builds with no new warnings.

## What comes next

- Task 1.5: Update EF Core configuration to map the new columns
- Task 2.1/2.2: Application layer services (OptimalRatioCalculator, FeasibilityEngine)
- Task 4.1: Update UpsertHomeLeaveConfigCommand to use the new domain methods

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): update HomeLeaveConfig entity with mode system and emergency freeze"
```
