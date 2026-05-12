# 175 — Onboarding Storage Module

## Phase

Feature: Onboarding Wizard — Storage Layer

## Purpose

Provides a pure utility module for reading and writing onboarding wizard state to localStorage. This is the persistence foundation for the entire onboarding feature — all other layers (Zustand store, detection hook, UI) depend on this module for state serialization, key formatting, and error recovery.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/onboarding/storage.ts` | Pure utility module with types, constants, and functions for onboarding localStorage persistence |

### Exports

- **Types:** `OnboardingStatus`, `StepCompletionMap`, `OnboardingState`
- **Constants:** `EMPTY_STEPS` — all five steps set to `false`
- **Functions:**
  - `getStorageKey(userId)` — returns `shifter-onboarding-{userId}`
  - `readOnboardingState(userId)` — JSON parse with error recovery (returns `null` on failure)
  - `writeOnboardingState(userId, state)` — silent failure if localStorage unavailable
  - `computeStatus(steps)` — returns `"completed"` if all true, else `"in-progress"`

## Key decisions

- **Pure functions only** — no side effects beyond localStorage access, making the module easy to test with property-based tests.
- **Silent failure pattern** — `writeOnboardingState` catches all errors silently to handle private browsing and quota-exceeded scenarios without crashing the app.
- **Null return on read failure** — corrupted JSON or missing keys return `null`, letting the caller treat it as a fresh state.
- **`computeStatus` only returns two values** — `"dismissed"` is never computed from steps; it's an explicit user action handled by the store layer.

## How it connects

- **Consumed by:** `useOnboardingStore` (Zustand store) for hydrate/persist operations
- **Consumed by:** Property-based tests (Properties 5 and 6)
- **Depends on:** Nothing (pure utility, no imports from other app modules)

## How to run / verify

```bash
# Type-check
cd apps/web && npx tsc --noEmit
```

## What comes next

- Task 1.2: Decision functions module (`decisions.ts`)
- Task 1.3: Property-based tests for storage and decision functions
- Task 2.2: Zustand store that uses this module for persistence

## Git commit

```bash
git add -A && git commit -m "feat(onboarding): add OnboardingStorage utility module"
```
