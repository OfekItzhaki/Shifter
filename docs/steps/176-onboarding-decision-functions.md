# 176 — Onboarding Decision Functions

## Phase

Onboarding Wizard — Storage & Decision Layer

## Purpose

Provides pure, testable decision functions that determine onboarding wizard behavior: whether to show the wizard, which step is current, how to map steps to routes, and how to compute step completion from application state. These functions are consumed by the Zustand store and UI components.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/onboarding/decisions.ts` | Pure decision functions module with `shouldShowOnboarding`, `getCurrentStepIndex`, `getStepRoute`, and `computeStepCompletion` |

## Key decisions

- **Pure functions only** — no side effects, no React hooks, no API calls. This makes them trivially testable with property-based tests.
- **`shouldShowOnboarding`** uses a two-condition check: groupCount must be 0 AND storage state must not be "completed" or "dismissed". A null storage state (first visit) results in showing the wizard.
- **`getCurrentStepIndex`** iterates the fixed step order and returns the first incomplete step index, or -1 if all are done.
- **`getStepRoute`** maps step keys to actual app routes. Steps that require a groupId fall back to the groups list page when no groupId is provided.
- **`computeStepCompletion`** uses `memberCount > 1` (not > 0) for the addMembers step because the group owner counts as 1 member.
- **Route format** uses the existing tab-based navigation pattern (`/groups/{groupId}?tab=members`) observed in the codebase.

## How it connects

- Imports types from `./storage` (the storage module created in task 1.1)
- Will be consumed by the Zustand onboarding store (task 2.2)
- Will be consumed by the OnboardingProvider component (task 5.2)
- Will be validated by property-based tests (task 1.3, Properties 1–4)

## How to run / verify

```bash
# Type check (from apps/web)
npx tsc --noEmit

# Property tests will be added in task 1.3
npx vitest run __tests__/onboarding.property.test.ts
```

## What comes next

- Task 1.3: Property-based tests validating all decision functions
- Task 2.2: Zustand store that uses these functions for state management
- Task 5.2: OnboardingProvider that calls `shouldShowOnboarding` on mount

## Git commit

```bash
git add -A && git commit -m "feat(onboarding): add decision functions module"
```
