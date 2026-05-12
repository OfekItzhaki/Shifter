# 178 — useStepCompletion Hook

## Phase

Onboarding Wizard

## Purpose

Provides a React hook that queries existing API clients to determine which onboarding steps are complete. This enables the onboarding wizard to reflect real application state (groups created, members added, tasks defined, etc.) without duplicating detection logic across components.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/hooks/useStepCompletion.ts` | Hook that fetches data from groups, members, tasks, constraints, and schedule APIs, then maps counts to a `StepCompletionMap` via `computeStepCompletion`. |

## Key decisions

- **No external query library** — uses `useState` + `useCallback` for simplicity, consistent with other hooks in the project (`useServiceWorker`, `usePushSubscription`).
- **On-demand refresh** — detection runs only when `refresh()` is called, not on mount. This avoids unnecessary API calls when the wizard isn't visible.
- **Graceful failure** — each API call is wrapped with `Promise.allSettled` so a single failing endpoint doesn't block the rest. Failed steps default to `false` (incomplete).
- **Group-scoped queries use first group** — for `addMembers` and `defineTasks`, the hook uses the first group returned by `getGroups`. If no groups exist, those steps remain false.
- **Skips when no space** — if `currentSpaceId` is null, `refresh()` is a no-op.

## How it connects

- Consumes `computeStepCompletion` from `lib/onboarding/decisions` (step 176).
- Consumes `StepCompletionMap` and `EMPTY_STEPS` from `lib/onboarding/storage` (step 175).
- Reads `currentSpaceId` from `useSpaceStore` (zustand store).
- Will be consumed by the `OnboardingWizard` component to display step progress.

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

The hook compiles cleanly with no type errors. Integration testing requires the full app running with API access.

## What comes next

- `OnboardingWizard` component that calls `refresh()` on mount and renders step progress.
- Auto-refresh after the user completes an action (e.g., creating a group).

## Git commit

```bash
git add -A && git commit -m "feat(onboarding): useStepCompletion hook for live step detection"
```
