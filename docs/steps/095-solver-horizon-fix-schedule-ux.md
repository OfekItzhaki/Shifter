# Step 095 — Solver Horizon Fix & Schedule UX Improvements

## Phase
Phase 9 — Quality & Polish

## Purpose
Two fixes:
1. The solver was generating shifts from the start of today (midnight UTC) instead of from the current time, producing past shifts. It was also capped at 7 days regardless of the group's configured horizon.
2. The schedule tab day buttons showed only "Sun/Mon/Tue..." with no date, making it hard to know which day you're looking at.

## What was fixed

### Solver horizon — starts from NOW, respects group setting
- `SolverPayloadNormalizer.cs`: `horizonStartDt` changed from `today.ToDateTime(TimeOnly.MinValue)` (midnight) to `DateTime.UtcNow` (current moment). Shifts that have already started or passed are no longer included.
- Removed the hardcoded `Math.Min(maxHorizon, 7)` cap. The solver now uses whatever `solverHorizonDays` the group owner configured (e.g. 14, 30 days). Replaced with `Math.Max(1, maxHorizon)` to ensure at least 1 day.
- `MaxShiftsPerTask` safety cap now scales with the horizon: `Math.Max(336, maxHorizon * 48)` instead of a fixed 336.
- `windowStart` for GroupTask shift generation now clamps to `nowUtc` (not midnight), so the first generated shift starts at or after the current time.

### Schedule tab — date + day name on day buttons
- Day buttons now show a two-line layout: abbreviated day name (small, muted) + date number (bold).
- Hebrew locale uses Hebrew day abbreviations (א׳–ש׳).
- A blue dot indicator marks today when it's not the selected day.
- A "Today" badge (localised) appears above the table when the selected day is today.
- The selected day's full date (e.g. "Monday, 4 May") is shown above the schedule table.

## Key decisions
- Starting from `DateTime.UtcNow` means the first shift slot may start mid-day, which is correct — there's no point scheduling shifts that are already over.
- Removing the 7-day cap is safe because the solver's CP-SAT model handles longer horizons; the group owner is responsible for setting a reasonable horizon in settings.
- Day buttons use `toLocaleDateString` with the user's locale for the full date label, so Hebrew/Russian users see the date in their language automatically.

## How to verify
1. Run the solver — the generated draft should only contain shifts starting from the current time onwards.
2. Open the schedule tab — day buttons should show day abbreviation + date number.
3. Click a day — the full date label should appear above the table.
4. Switch to Hebrew locale — day abbreviations should be in Hebrew.

## Git commit

```bash
git add -A && git commit -m "fix(solver): start horizon from now not midnight, remove 7-day cap; feat(schedule): date+day on day buttons"
```
