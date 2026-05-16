# 268 — Home-Leave Frontend Integration

## Phase

Home-Leave Overhaul — Frontend Integration (Tasks 9.1–9.4)

## Purpose

Wire all new home-leave UI components together into the main `HomeLeaveConfigPanel`, update the API client types to match the new controller DTOs, verify RTL/LTR slider handling, and ensure i18n translations are complete for all three languages.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/homeLeave.ts` | Updated API client with new `HomeLeaveConfigDto` (includes mode, baseDays, homeDays, emergency fields, optimal ratio), `UpdateHomeLeaveConfigPayload`, `getOptimalRatio`, `toggleEmergencyFreeze`, and updated preview types |
| `apps/web/hooks/useHomeLeavePreview.ts` | Updated hook to accept a `HomeLeavePreviewRequest` object instead of a single `balanceValue` number |
| `apps/web/components/home-leave/HomeLeaveConfigPanel.tsx` | Complete rewrite integrating ModeSelector, RatioSlider, ManualModeSection, EmergencyFreezeBanner, FeasibilityIndicator, and LeaveDurationInput |
| `apps/web/messages/he.json` | Added `homeLeave.panel` translation keys (title, description, loading, save, saving, saved, permissionError) |
| `apps/web/messages/en.json` | Added `homeLeave.panel` translation keys |
| `apps/web/messages/ru.json` | Added `homeLeave.panel` translation keys |

## Key decisions

1. **RatioSlider RTL already correct** — The component already uses `useLocale()`, `dir` attribute, and flipped gradient. No changes needed (task 9.1 verified).
2. **Emergency freeze toggle is a separate API call** — Uses the same PUT endpoint but with `emergencyFreezeActive` flag, persisting immediately without requiring the user to click Save.
3. **Preview hook generalized** — Changed from accepting a single `balanceValue` to a full `HomeLeavePreviewRequest` object, supporting both automatic (sliderValue) and manual (baseDays/homeDays) modes.
4. **Panel hides mode controls during freeze** — When emergency freeze is active, the mode selector and ratio/manual inputs are hidden since they're irrelevant.
5. **ImpactSummary and BalanceSlider are now dead code** — No longer imported by the panel. They can be removed in task 11.2.

## How it connects

- Depends on: Task 8 (core components), Task 4 (API endpoints), Task 6 (solver payload)
- Used by: The group detail page renders `HomeLeaveConfigPanel` when `isClosedBase` is true
- Feeds into: Task 9.5/9.6 (property tests), Task 11.2 (cleanup of old BalanceSlider)

## How to run / verify

1. Navigate to a closed-base group's detail page
2. Verify the panel shows: Emergency Freeze banner → Mode Selector → Slider or Manual inputs → Leave Duration → Save button
3. Switch between Automatic and Manual modes — verify correct section shows
4. Move the slider — verify feasibility indicator updates after 500ms debounce
5. In Manual mode, change days — verify feasibility updates
6. Activate emergency freeze — verify mode controls hide, red banner shows with timer
7. Deactivate freeze — verify previous mode restores
8. Switch to Hebrew locale — verify RTL slider direction and all labels are translated
9. Switch to Russian — verify all labels are translated

## What comes next

- Task 9.5: Property test for slider monotonicity (fast-check)
- Task 9.6: Property test for emergency freeze state restoration (fast-check)
- Task 11.2: Remove old `BalanceSlider.tsx` and `ImpactSummary.tsx`

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): frontend integration — panel, API client, i18n (tasks 9.1-9.4)"
```
