# Step 623 — Pick Group Sorting Property Test

## Phase

Feature: shift-picker-lite — Property-based testing

## Purpose

Validates Property 4 from the shift-picker-lite design: "Group list sorting is stable and locale-aware." This ensures that for any list of self-service groups, the sorted output from `filterSelfServiceGroups` is in ascending order by name using Hebrew locale comparison.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/selfService/pickGroupFilter.sorting.property.test.ts` | Property-based test using fast-check that generates arbitrary arrays of self-service groups with random Hebrew/English names and asserts the output is sorted in ascending Hebrew locale order |

## Key decisions

- Used `fast-check` with 100 iterations as specified in the design document
- Generated group names from a character set including Hebrew letters, English letters, digits, and spaces to cover realistic input space
- Asserted the sorting invariant: for every consecutive pair (a, b), `a.name.localeCompare(b.name, "he") <= 0`
- All generated groups have `schedulingMode === "SelfService"` so the filter passes them through and we test only the sorting behavior

## How it connects

- Validates the sorting behavior of `filterSelfServiceGroups` in `apps/web/lib/utils/pickGroupFilter.ts`
- Complements the unit tests in `pickGroupFilter.test.ts` (task 2.1) and the filtering property test (task 2.2)
- Validates Requirement 2.1 (groups sorted by name ascending)

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/selfService/pickGroupFilter.sorting.property.test.ts
```

## What comes next

- Task 3: Checkpoint — Utility layer complete
- Tasks 5–9: UI components and route wiring

## Git commit

```bash
git add -A && git commit -m "feat(shift-picker-lite): property test for group sorting (Property 4)"
```
