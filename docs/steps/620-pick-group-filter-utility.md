# Step 620 — Pick Group Filter Utility

## Phase

Shift Picker Lite — Core utilities

## Purpose

Provides a reusable utility function that filters a list of groups to only those with `schedulingMode === "SelfService"` and sorts them by name ascending using Hebrew locale comparison. This is used by the `/pick` route to determine which groups to display in the group selector.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/utils/pickGroupFilter.ts` | `filterSelfServiceGroups` function — filters and sorts groups |
| `apps/web/__tests__/selfService/pickGroupFilter.test.ts` | Unit tests covering filtering, sorting, edge cases |

## Key decisions

- Uses `Array.filter` + `Array.sort` with `localeCompare("he")` for Hebrew-aware alphabetical ordering
- Returns a new array (does not mutate the input)
- Imports `GroupWithMemberCountDto` from the existing `@/lib/api/groups` module

## How it connects

- Used by the `PickPage` component (task 8.1) to resolve which groups to show in the group selector
- Depends on `GroupWithMemberCountDto` from `lib/api/groups.ts`
- Property tests (tasks 2.2, 2.3) will validate this function's correctness properties

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/selfService/pickGroupFilter.test.ts
```

## What comes next

- Property tests for group filtering (task 2.2) and sorting (task 2.3)
- GroupSelector component (task 6.1) will use this utility

## Git commit

```bash
git add -A && git commit -m "feat(shift-picker-lite): add pickGroupFilter utility with unit tests"
```
