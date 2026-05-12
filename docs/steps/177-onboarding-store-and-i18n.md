# 177 — Onboarding Zustand Store & i18n Keys

## Phase

Onboarding Wizard — State Layer & Internationalization

## Purpose

Provides the runtime state management for the onboarding wizard via a Zustand store, and adds all user-facing translation keys for the three supported locales (en, he, ru). The store bridges the pure storage utilities with the UI layer, while the i18n keys enable the panel to render localized text.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/store/onboardingStore.ts` | Zustand store with visibility, steps, status state and actions (show, hide, dismiss, completeStep, reset, hydrate, setSteps) |
| `apps/web/messages/en.json` | Added `onboarding` namespace with all step titles, descriptions, CTAs, and panel-level strings |
| `apps/web/messages/he.json` | Hebrew translations for the `onboarding` namespace |
| `apps/web/messages/ru.json` | Russian translations for the `onboarding` namespace |

## Key decisions

- **No persist middleware** — The store manually calls `readOnboardingState` / `writeOnboardingState` from the storage module, matching the design doc's explicit architecture. This keeps the storage layer testable and decoupled.
- **Spread `EMPTY_STEPS`** — Each action that resets steps creates a new object via spread to avoid shared references.
- **`computeStatus` reuse** — Both `completeStep` and `setSteps` recompute status via the pure `computeStatus` function, ensuring consistency.
- **Translation key format** — Uses `onboarding.steps.{stepKey}.title/description/cta` matching the step config module's `titleKey`/`descriptionKey`/`ctaLabelKey` pattern.

## How it connects

- **Depends on**: `lib/onboarding/storage.ts` (types and functions), `lib/onboarding/steps.ts` (step config references these i18n keys)
- **Used by**: `OnboardingProvider` (hydrates on mount), `OnboardingPanel` (reads state and dispatches actions), `useStepCompletion` hook (calls `setSteps`)

## How to run / verify

1. TypeScript diagnostics pass with no errors on the store file
2. All three JSON locale files parse as valid JSON
3. The `onboarding` namespace keys are consistent across all three locales

## What comes next

- Task 2.3: Unit tests for the Zustand store
- Task 5.1/5.2: UI components that consume the store and translation keys

## Git commit

```bash
git add -A && git commit -m "feat(onboarding): add Zustand store and i18n keys for all locales"
```
