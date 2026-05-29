# 622 — Pick Last-Group Memory Round-Trip Property Test

## Phase

Feature: shift-picker-lite — Property-based testing

## Purpose

Validates that the `resolveLastGroup` function correctly round-trips any valid group ID that exists in the member's self-service groups list. This property test ensures the last-group memory mechanism reliably returns the stored group ID when it is present in the available groups, covering the full input space via fast-check.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/selfService/pickLastGroup.property.test.ts` | Property-based test using fast-check (100+ iterations) that generates arbitrary UUIDs and group lists, asserts `resolveLastGroup(groupId, groups)` returns the same groupId when the group is in the list |

## Key decisions

- Used `fc.uuid()` for generating valid UUID strings and `fc.chain` to guarantee the target UUID is always present in the generated groups list
- Minimum 100 iterations as specified in the design document
- Followed existing project patterns (see `formatTime.property.test.ts`) for file naming and structure
- Test validates Requirements 3.1 (last-group memory returns stored group) and 3.4 (switching groups updates memory correctly)

## How it connects

- Tests the `resolveLastGroup` function from `apps/web/lib/utils/pickLastGroup.ts` (created in task 1.1)
- Complements the example-based unit tests in `pickLastGroup.test.ts`
- Part of the Property 1 correctness guarantee from the design document

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/selfService/pickLastGroup.property.test.ts
```

## What comes next

- Task 1.3: Property test for invalid last-group memory clearing (Property 2)
- Tasks 2.2, 2.3: Property tests for group filtering and sorting

## Git commit

```bash
git add -A && git commit -m "feat(shift-picker-lite): property test for last-group memory round-trip"
```
