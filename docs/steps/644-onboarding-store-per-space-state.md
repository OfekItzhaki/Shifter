# 644 — Onboarding Store Per-Space State

## Phase

Space-First Onboarding (Task 15)

## Purpose

The onboarding setup guide tracks which steps a user has completed (create group, add members, define tasks, set constraints, run solver). Previously, this state was stored per-user only. With multi-space support, each space needs independent onboarding progress so that joining or creating a new space shows a fresh setup guide.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/store/onboardingStore.ts` | Updated all store methods (`hydrate`, `completeStep`, `reset`, `dismiss`, `setSteps`) to accept an optional `spaceId` parameter, which is forwarded to the storage read/write functions. |
| `apps/web/components/onboarding/OnboardingProvider.tsx` | Passes `currentSpaceId` from `spaceStore` to `hydrate`, `setSteps`, and `readOnboardingState` calls. |
| `apps/web/components/onboarding/OnboardingPanel.tsx` | Passes `currentSpaceId` to `dismiss` so dismissal is persisted per-space. |
| `apps/web/app/home/page.tsx` | Passes `currentSpaceId` to `resetOnboarding` so restart is scoped to the current space. |

The underlying `lib/onboarding/storage.ts` already supported `spaceId` in `getStorageKey`, `readOnboardingState`, and `writeOnboardingState` — this change threads the value through from all consumers.

## Key decisions

- `spaceId` is optional (`spaceId?: string`) to maintain backward compatibility — if no space is selected yet, the store falls back to the user-only key.
- The localStorage key format is `shifter-onboarding-${userId}-${spaceId}` when a space is active, matching the design doc specification.
- No migration of existing localStorage entries — users who already completed onboarding before multi-space will simply see a fresh guide for new spaces.

## How it connects

- **spaceStore** provides `currentSpaceId` which is the active space context.
- **OnboardingProvider** is mounted in the app layout and auto-hydrates on space change.
- **OnboardingPanel** renders the setup guide checklist and uses dismiss per-space.
- **Home page** allows restarting the guide, now scoped to the current space.

## How to run / verify

1. Log in and select a space — the setup guide should show per-space progress.
2. Complete a step in Space A, switch to Space B — Space B should show all steps incomplete.
3. Dismiss the guide in Space A, switch to Space B — the guide should still be visible in Space B.
4. Restart the guide from the home page — only the current space's progress resets.

## What comes next

- Task 16: Solver Integration — Parent Schedule Cascading

## Git commit

```bash
git add -A && git commit -m "feat(onboarding): per-space onboarding state via spaceId-scoped localStorage key"
```
