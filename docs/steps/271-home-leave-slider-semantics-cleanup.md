# 271 — Home-Leave Slider Semantics Cleanup

## Phase

Home-Leave Overhaul — Refinement

## Purpose

Align the RatioSlider's `calculateDisplayRatio` function with the correct semantic model and remove dead code from the config panel. The slider now uses clean linear formulas:
- Center (50) = optimal ratio
- Left of center = fewer home days (conservative — shorter visits)
- Right of center = more base days (conservative — longer wait between visits)

Also removes the unused `restHoursAfterReturn` state and stale `LeaveDurationInput` comment from the config panel, since leave duration is now always derived from `homeDays * 24`.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/home-leave/RatioSlider.tsx` | Replaced `calculateDisplayRatio` with correct linear formulas: left decreases homeDays via `max(1, round(optimalHomeDays * (value / 50)))`, right increases baseDays via `round(optimalBaseDays * (1 + (value - 50) / 50))` |
| `apps/web/components/home-leave/HomeLeaveConfigPanel.tsx` | Removed unused `restHoursAfterReturn` state variable and setter; removed stale `LeaveDurationInput` mention from JSDoc comment |

## Key decisions

- The old formula used arbitrary multipliers (1.5× for right, 0.8× for left) that didn't match the spec. The new formula is a clean linear interpolation: right goes from 1× to 2× optimal base days, left goes from optimal home days down to 1.
- `LeaveDurationInput` component file is kept for potential reuse elsewhere but is not imported or rendered in the home-leave panel.
- `leave_duration_hours` remains in the DB/API for backward compatibility but is always computed as `homeDays * 24` in the save payload (already implemented in prior step).

## How it connects

- The slider display now matches what the backend's `SetSliderPosition` domain method computes, ensuring the admin sees the same ratio the solver will use.
- The save payload in `HomeLeaveConfigPanel` already sends `leaveDurationHours: homeDays * 24`, so no additional save logic changes were needed.

## How to run / verify

```bash
cd apps/web && npx tsc --noEmit   # TypeScript passes
cd apps/api && dotnet build --no-restore -v q   # .NET builds cleanly
```

Manual verification: open the home-leave config panel for a closed-base group, move the slider left/right, and confirm the displayed ratio changes linearly as described.

## What comes next

- Property tests for the `calculateDisplayRatio` function to verify boundary behavior (value=0, 50, 100).
- Potential removal of the `LeaveDurationInput.tsx` file if confirmed unused elsewhere.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): update slider formula and remove dead config state"
```
