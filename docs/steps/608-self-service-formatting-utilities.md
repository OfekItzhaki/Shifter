# 608 — Self-Service Formatting Utilities

## Phase

Self-Service Scheduling UI — Foundation Layer

## Purpose

Provides Hebrew-locale date formatting, 24-hour time display, countdown timers for waitlist/swap expiry, and capacity visual classification for the slot browser. These utilities are used by all self-service tab components to render dates, times, and capacity indicators consistently.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/utils/selfServiceFormat.ts` | Formatting utility module with `HEBREW_DAY_NAMES`, `formatSlotDate`, `formatTime24h`, `formatCountdown`, `getCapacityClass` |

## Key decisions

- **`HEBREW_DAY_NAMES` indexed by `getDay()`**: Matches JavaScript's native day-of-week numbering (0=Sunday) for direct lookup without offset math.
- **`formatSlotDate` uses `he-IL` locale**: Leverages `Intl` for locale-aware date formatting, prepending the Hebrew day name for readability.
- **`formatTime24h` handles multiple input formats**: Accepts both raw time strings (`HH:mm`, `HH:mm:ss`) and full ISO datetime strings for flexibility across different API response shapes.
- **`formatCountdown` switches units at 24h boundary**: Shows days+hours for long countdowns and hours+minutes for short ones, matching the design spec.
- **`getCapacityClass` uses > 50% threshold**: Returns `"high-availability"` when remaining capacity exceeds 50%, `"nearly-full"` otherwise, as specified in requirement 5.10.
- **Graceful fallback**: All functions return `"—"` for invalid/empty input, consistent with existing utility patterns in the codebase.

## How it connects

- Used by `SlotBrowserTab` for date/time display and capacity styling (requirements 5.2, 5.10)
- Used by `MyShiftsTab`, `WaitlistTab`, `SwapsTab` for date/time formatting (requirements 6.2, 7.1, 8.1)
- Used by `WaitlistTab` and `SwapsTab` for countdown display (requirements 7.2, 8.10)
- Property tests in task 2.4 will validate Properties 11, 12, 13 from the design document

## How to run / verify

```bash
# Type-check the file
cd apps/web && npx tsc --noEmit lib/utils/selfServiceFormat.ts
```

## What comes next

- Task 2.4: Property-based tests for formatting functions (Properties 11, 12, 13)
- Tab components (tasks 5–8) will import and use these utilities

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): add self-service formatting utilities"
```
