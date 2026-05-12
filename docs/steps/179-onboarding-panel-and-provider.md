# 179 — Onboarding Panel & Provider Components

## Phase

Phase 9 — Onboarding Wizard

## Purpose

Provides the UI layer for the onboarding wizard: a floating checklist panel that guides new users through their first 5 setup steps, and a provider component that manages the onboarding lifecycle (detection, hydration, step completion sync).

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/onboarding/OnboardingPanel.tsx` | Floating bottom-right dialog showing step progress, CTA buttons, and success state |
| `apps/web/components/onboarding/OnboardingProvider.tsx` | Lifecycle wrapper that hydrates the store, detects whether onboarding should show, and syncs step completion |

## Key decisions

- **RTL support**: Panel uses `inset-inline-end` instead of `right` for correct positioning in RTL locales.
- **Progressive disclosure**: Only the current (first incomplete) step shows its description and CTA button, keeping the panel compact.
- **Non-blocking**: The provider silently catches API errors — onboarding is non-critical and should never break the app.
- **Keyboard accessible**: Escape key dismisses the panel; semantic `role="dialog"` and `aria-live="polite"` for screen readers.
- **No wrapper div**: The provider renders `{children}` directly without adding DOM nodes.
- **Re-evaluation on visibility**: When the panel becomes visible again, it re-fetches step completion to reflect any actions the user took while the panel was hidden.

## How it connects

- Depends on: `useOnboardingStore` (step 177), `ONBOARDING_STEPS` (step 175), decision functions (step 176), `useStepCompletion` hook (step 178), `useAuthStore`, `useSpaceStore`, groups API.
- Consumed by: The app shell or layout — `OnboardingProvider` wraps the app tree, `OnboardingPanel` renders inside it.
- Translation keys live under the `onboarding` namespace in `messages/en.json` and `messages/he.json`.

## How to run / verify

1. Wrap the app with `<OnboardingProvider>` and render `<OnboardingPanel />` inside the layout.
2. Log in as a new user with no groups — the panel should appear at bottom-right.
3. Verify steps show checkmarks when completed, CTA navigates to the correct route.
4. Press Escape or click X to dismiss — panel should hide and persist dismissal.
5. TypeScript: `npx tsc --noEmit` passes with no errors on both files.

## What comes next

- Integration into the app layout (render `OnboardingProvider` + `OnboardingPanel` in the root layout or `AppShell`).
- Restart button in the sidebar to re-show the panel after dismissal.
- E2E tests for the onboarding flow.

## Git commit

```bash
git add -A && git commit -m "feat(onboarding): add OnboardingPanel and OnboardingProvider components"
```
