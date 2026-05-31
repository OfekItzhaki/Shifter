# Step 649 — Wire Rest Duration Display into ScheduleTaskTable

## Phase

Feature — Rest Duration Display

## Purpose

Integrates the rest duration computation and badge rendering into the existing `ScheduleTaskTable` component. Admins can now see color-coded rest duration indicators below each person's name, showing how much rest they have before their next assignment. This is the core integration step that connects the utility layer (computation) and presentational layer (badge) to the schedule view.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/schedule/ScheduleTaskTable.tsx` | Added `minRestHours` optional prop, memoized `computeRestDurations` call, lookup map construction, and conditional `RestDurationBadge` rendering in person cells |

## Key decisions

- **Type narrowing** — `TaskAssignment.personId` is `string | undefined` while `RestDurationInput.personId` requires `string`. We filter out assignments without a `personId` using a type guard before passing to `computeRestDurations`.
- **Memoization with `useMemo`** — `computeRestDurations` is called once per render cycle and only recomputes when `assignments`, `isAdmin`, or `minRestHours` change. This avoids redundant O(n log n) sorting on every render.
- **Early return in useMemo** — when `isAdmin` is false or `minRestHours` is null, returns an empty map immediately to avoid unnecessary computation for non-admin users.
- **Lookup map keyed by `${personId}|${slotStartsAt}`** — provides O(1) access during render. The key matches the slot's `startsAt` field which is the same ISO string used in the assignment data.
- **Flex column layout** — changed the person cell from a single-row flex to a `flex-col` layout with `gap-0.5` to stack the badge below the person name + action button row.
- **IIFE pattern for conditional rendering** — used an immediately-invoked function to perform the map lookup and conditionally return the badge, keeping the JSX clean.
- **Guard conditions** — badge only renders when all three conditions are met: `isAdmin`, `minRestHours != null`, and a rest entry exists for that person+slot combination.

## How it connects

- **Depends on**: `apps/web/lib/utils/restDuration.ts` (`computeRestDurations`, `RestDurationEntry`)
- **Depends on**: `apps/web/components/schedule/RestDurationBadge.tsx`
- **Consumed by**: Parent schedule page (task 3.3) will pass `minRestHours` from group constraints
- **Validates**: Requirements 2.1, 2.2, 2.3, 3.1, 4.4

## How to run / verify

```bash
# Type-check the component
cd apps/web && npx tsc --noEmit
```

Or verify via the IDE — the file should show zero TypeScript errors. The component accepts the new `minRestHours` prop and renders badges when conditions are met.

## What comes next

- Task 3.3: Pass `minRestHours` from the parent schedule page to `ScheduleTaskTable`
- Task 3.4: Unit tests for the integration (admin visibility, badge presence, edge cases)

## Git commit

```bash
git add -A && git commit -m "feat(rest-duration): wire rest duration display into ScheduleTaskTable"
```
