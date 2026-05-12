# 175 — Onboarding Step Configuration Module

## Phase

Feature — Onboarding Wizard

## Purpose

Defines the static configuration for the five onboarding steps (create group, add members, define tasks, set constraints, run solver). This module provides a typed array that the UI layer consumes to render the checklist panel with correct i18n keys and icons for each step.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/onboarding/steps.ts` | `OnboardingStepConfig` interface and `ONBOARDING_STEPS` constant array |
| `apps/web/lib/onboarding/storage.ts` | Storage utility module with types (`StepCompletionMap`, `OnboardingState`, `OnboardingStatus`), `EMPTY_STEPS` constant, and functions (`getStorageKey`, `readOnboardingState`, `writeOnboardingState`, `computeStatus`) |

## Key decisions

- **Emoji icons** — Used emoji identifiers (👥, ➕, 📋, ⚙️, 🚀) for step icons. These are simple, cross-platform, and don't require additional SVG assets.
- **next-intl key format** — Keys follow `onboarding.steps.{stepKey}.title`, `.description`, `.cta` pattern for consistency with the i18n namespace.
- **Type safety via StepCompletionMap** — The `key` field in `OnboardingStepConfig` is typed as `keyof StepCompletionMap`, ensuring only valid step keys can be used.
- **Storage module created alongside** — Since task 2.1 requires importing `StepCompletionMap` from `./storage`, the full storage module was implemented as part of this step (covers task 1.1 as well).

## How it connects

- **Consumed by** `OnboardingPanel` component to render the step list
- **Consumed by** `useStepCompletion` hook to map step keys to detection logic
- **Depends on** `StepCompletionMap` type from `./storage` for type-safe step keys
- **i18n keys** referenced here must be defined in `messages/en.json`, `messages/he.json`, `messages/ru.json` (task 4.2)

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit --strict apps/web/lib/onboarding/steps.ts
```

Or verify via IDE — no TypeScript errors should appear in either file.

## What comes next

- Task 2.2: Zustand onboarding store (imports from `./storage`)
- Task 4.2: i18n translation keys matching the key format defined here
- Task 5.1: OnboardingPanel component consuming `ONBOARDING_STEPS`

## Git commit

```bash
git add -A && git commit -m "feat(onboarding): step configuration and storage utility modules"
```
