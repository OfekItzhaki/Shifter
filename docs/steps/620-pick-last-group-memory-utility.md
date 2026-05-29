# Step 620 — Pick Last-Group Memory Utility

## Phase

Shift Picker Lite — Core utilities and last-group memory logic

## Purpose

Provides localStorage-based persistence for the member's last selected self-service group in the `/pick` route. Returning members skip the group selector and go straight to their last-used group's slot browser. The utility also validates stored group IDs (UUID format + existence in the member's current groups) to handle stale or corrupted values gracefully.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/utils/pickLastGroup.ts` | Utility module with `LAST_GROUP_KEY` constant, `getLastGroup`, `setLastGroup`, `clearLastGroup`, and `resolveLastGroup` functions |
| `apps/web/__tests__/selfService/pickLastGroup.test.ts` | Unit tests covering all functions, edge cases (null, empty, whitespace, invalid UUID, missing group), and happy paths |

## Key decisions

- **Try/catch around localStorage calls**: Handles private browsing mode and quota-exceeded errors without crashing the app.
- **UUID regex validation**: Uses a standard UUID v4 regex (case-insensitive) to reject obviously invalid stored values before hitting the groups list.
- **Trim check for empty values**: Both `getLastGroup` and `resolveLastGroup` treat whitespace-only strings as empty/null.
- **No side effects in `resolveLastGroup`**: The function is pure — it doesn't clear localStorage itself. The caller decides what to do when it returns null.

## How it connects

- Used by the `PickPage` route component (task 8.1) to determine whether to show the group selector or skip to the slot browser.
- `setLastGroup` is called when a member selects a group (task 8.2).
- `clearLastGroup` is called when `resolveLastGroup` returns null (stale/invalid stored value).
- Property-based tests (tasks 1.2, 1.3) will exercise `resolveLastGroup` with generated inputs.

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/selfService/pickLastGroup.test.ts --reporter=verbose
```

All 19 tests should pass.

## What comes next

- Task 1.2: Property test for last-group memory round-trip
- Task 1.3: Property test for invalid last-group memory clearing
- Task 2.1: Group filtering utility (`pickGroupFilter.ts`)

## Git commit

```bash
git add -A && git commit -m "feat(shift-picker-lite): last-group memory utility module"
```
