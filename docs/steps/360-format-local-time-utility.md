# 360 — formatLocalTime Utility

## Phase

Feature: User Timezone Settings — Frontend time formatting

## Purpose

Provides a centralized, timezone-aware time formatting utility that all frontend components use to display UTC datetimes in the user's local timezone. Uses `Intl.DateTimeFormat` with the IANA `timeZone` option for correct DST handling, rather than a fixed numeric offset.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/utils/formatTime.ts` | Core utility with `formatLocalTime`, `formatLocalDateTime`, `formatLocalDate`, `getLocalTimeParts`, and `toUtcIsoString` functions |
| `apps/web/__tests__/formatTime.test.ts` | 32 unit tests covering timezone conversion, DST handling, 12h/24h formats, edge cases, and UTC preservation |

## Key decisions

1. **`Intl.DateTimeFormat` with `timeZone` option** — Uses the browser's built-in timezone database via the IANA identifier. This correctly handles DST transitions without manual offset math.
2. **Default timezone: `Asia/Jerusalem`** — When `timezoneId` is null/undefined/empty, falls back to the app's primary user base timezone.
3. **Graceful error handling** — Returns "—" (em dash) for any invalid input (null, undefined, invalid date strings, invalid timezone IDs) rather than throwing.
4. **`toUtcIsoString` identity function** — Documents the intent that outgoing API values are always UTC. Validates and normalizes the string but never applies a client-side offset.
5. **Separate from existing `dateFormat.ts`** — The existing utility handles locale-based formatting without timezone awareness. This new utility is specifically for timezone-aware display using the IANA ID from the auth store.

## How it connects

- **Consumes**: `timezoneId` from `authStore` (set during login/refresh in task 6.1)
- **Consumed by**: All time-rendering components (task 7.1), schedule views, assignment times, log timestamps
- **Property tests**: Task 6.3 will add fast-check property tests for DST-aware display (Property 7) and UTC preservation (Property 8)
- **API contract**: `toUtcIsoString` ensures outgoing requests never apply client-side offset (Requirement 5.4)

## How to run / verify

```bash
cd apps/web
npx vitest --run __tests__/formatTime.test.ts
```

All 32 tests should pass, covering:
- Basic timezone conversion (3 timezones)
- DST transitions (summer vs winter offsets)
- 12h/24h format support
- Default timezone fallback
- Edge cases (null, invalid dates, invalid timezones)
- UTC preservation for outgoing requests

## What comes next

- **Task 6.3**: Property-based tests for `formatLocalTime` (Properties 7 and 8)
- **Task 7.1**: Replace all direct date formatting across components with `formatLocalTime` using `timezoneId` from authStore

## Git commit

```bash
git add -A && git commit -m "feat(timezone): create formatLocalTime utility with Intl.DateTimeFormat DST support"
```
