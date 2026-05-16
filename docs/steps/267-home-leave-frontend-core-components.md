# 267 — Home-Leave Frontend Core Components

## Phase

Home-Leave Overhaul — Frontend (Task 8)

## Purpose

Create the six core UI components for the new home-leave configuration system: ModeSelector, RatioSlider, ManualModeSection, EmergencyFreezeBanner, FeasibilityIndicator, and LeaveDurationInput. These replace the old single-slider approach with a three-mode system that admins can understand intuitively.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/home-leave/ModeSelector.tsx` | Segmented control (Automatic / Manual) with ARIA radiogroup, keyboard navigation, and i18n labels |
| `apps/web/components/home-leave/RatioSlider.tsx` | Smart slider centered on optimal ratio, gradient background, RTL/LTR support, step=5, Page Up/Down ±10 |
| `apps/web/components/home-leave/ManualModeSection.tsx` | Two numeric inputs (base days, home days) with min-1 validation and 500ms debounced feasibility check |
| `apps/web/components/home-leave/EmergencyFreezeBanner.tsx` | Prominent toggle with confirmation dialog, live duration timer, and "use for scheduling" checkbox |
| `apps/web/components/home-leave/FeasibilityIndicator.tsx` | Green/red indicator with localized explanation, loading state |
| `apps/web/components/home-leave/LeaveDurationInput.tsx` | Days input (0.5–7) that stores internally as hours (×24), with validation |
| `apps/web/messages/he.json` | Added `homeLeave` section with Hebrew translations |
| `apps/web/messages/en.json` | Added `homeLeave` section with English translations |
| `apps/web/messages/ru.json` | Added `homeLeave` section with Russian translations |

## Key decisions

- **RTL handling in RatioSlider**: Uses `dir` attribute on the container and flips gradient direction. Value semantics (0=conservative, 100=generous) remain consistent regardless of direction.
- **Debounced feasibility**: ManualModeSection debounces API calls by 500ms using a ref-based timeout to avoid excessive server calls during rapid input.
- **Emergency freeze confirmation**: Activation requires explicit confirmation dialog; deactivation is immediate (no confirmation needed to restore normal operations).
- **LeaveDurationInput stores hours**: The component displays days to the user but emits hours internally, matching the backend's `leaveDurationHours` field.
- **FeasibilityIndicator is standalone**: Extracted as its own component so it can be reused in both Automatic and Manual mode sections.

## How it connects

- These components will be wired together in Task 9.2 (HomeLeaveConfigPanel integration)
- The RatioSlider replaces the existing BalanceSlider (removal in Task 11.2)
- ManualModeSection's `onCheckFeasibility` prop will call the feasibility preview API (Task 9.3)
- EmergencyFreezeBanner's callbacks will call the emergency freeze API endpoints (Task 9.2)
- All components use the `homeLeave.*` translation namespace added to all three locale files

## How to run / verify

```bash
# Check TypeScript compilation
cd apps/web && npx tsc --noEmit

# Verify no lint errors
npx next lint
```

All components pass TypeScript diagnostics with zero errors.

## What comes next

- Task 9.1: RTL/LTR direction handling integration in RatioSlider
- Task 9.2: Wire all components into HomeLeaveConfigPanel
- Task 9.3: Update API client types and functions
- Task 9.4: Complete i18n translation keys for remaining UI strings

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): core frontend components — ModeSelector, RatioSlider, ManualModeSection, EmergencyFreezeBanner, FeasibilityIndicator, LeaveDurationInput"
```
