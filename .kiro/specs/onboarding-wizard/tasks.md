# Implementation Plan: Onboarding Wizard

## Overview

Implement a frontend-only onboarding wizard as a floating checklist panel that guides new users through five core setup steps. The implementation follows a layered architecture: storage utilities → pure decision functions → Zustand store → detection hook → UI components → AppShell integration. All user-facing text uses next-intl keys with support for en, he, and ru locales.

## Tasks

- [x] 1. Implement storage layer and decision functions
  - [x] 1.1 Create OnboardingStorage utility module
    - Create `apps/web/lib/onboarding/storage.ts`
    - Implement `OnboardingState`, `StepCompletionMap`, and `OnboardingStatus` types
    - Implement `EMPTY_STEPS` constant
    - Implement `getStorageKey(userId)` returning `shifter-onboarding-{userId}`
    - Implement `readOnboardingState(userId)` with JSON parse, error recovery (return null on failure)
    - Implement `writeOnboardingState(userId, state)` with silent failure on localStorage unavailability
    - Implement `computeStatus(steps)` returning "completed" if all true, else "in-progress"
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 1.2 Create decision functions module
    - Create `apps/web/lib/onboarding/decisions.ts`
    - Implement `shouldShowOnboarding(ctx)` — returns true iff groupCount === 0 AND storage state is neither "completed" nor "dismissed"
    - Implement `getCurrentStepIndex(steps)` — returns index of first false step, or -1 if all complete
    - Implement `getStepRoute(stepKey, spaceId, groupId?)` — maps step keys to navigation routes
    - Implement `computeStepCompletion(appState)` — maps counts to booleans (memberCount > 1 for addMembers)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.4, 3.1, 3.2, 3.3, 3.4, 3.5, 4.1, 4.2, 4.3, 4.4, 4.5_

  - [ ]* 1.3 Write property tests for storage and decision functions
    - **Property 1: Display Decision Logic** — shouldShowOnboarding returns true iff groupCount=0 and state is not completed/dismissed
    - **Property 2: Step Completion from Application State** — computeStepCompletion maps counts > 0 to true (memberCount > 1)
    - **Property 3: Current Step Index** — getCurrentStepIndex returns first false index or -1
    - **Property 4: Step-to-Route Mapping** — getStepRoute returns valid route starting with / containing spaceId
    - **Property 5: Storage Round-Trip** — write then read produces deeply equal state
    - **Property 6: Status Computation** — computeStatus returns "completed" iff all five steps true
    - **Property 7: Reset Produces Clean State** — reset always produces in-progress with all steps false
    - Create test file at `apps/web/__tests__/onboarding.property.test.ts`
    - Use fast-check with minimum 100 iterations per property
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 2.4, 3.1–3.5, 4.1–4.6, 5.4, 6.1, 6.2, 6.3, 10.2, 10.3**

- [x] 2. Implement step configuration and Zustand store
  - [x] 2.1 Create step configuration module
    - Create `apps/web/lib/onboarding/steps.ts`
    - Define `OnboardingStepConfig` interface with key, titleKey, descriptionKey, ctaLabelKey, icon
    - Export `ONBOARDING_STEPS` array with 5 steps in order: createGroup, addMembers, defineTasks, setConstraints, runSolver
    - Use next-intl key format: `onboarding.steps.{stepKey}.title`, `.description`, `.cta`
    - _Requirements: 2.1, 2.2, 7.1_

  - [x] 2.2 Create Zustand onboarding store
    - Create `apps/web/lib/store/onboardingStore.ts`
    - Implement `useOnboardingStore` with state: isVisible, steps, status
    - Implement actions: show, hide, dismiss(userId), completeStep(userId, stepKey), reset(userId), hydrate(userId), setSteps(userId, steps)
    - `dismiss` writes "dismissed" status to storage and sets isVisible to false
    - `completeStep` updates the step, recomputes status via `computeStatus`, writes to storage
    - `reset` sets all steps to false, status to "in-progress", writes to storage
    - `hydrate` reads from storage and populates store state
    - _Requirements: 4.6, 5.2, 5.3, 6.1, 6.2, 6.3, 10.2_

  - [ ]* 2.3 Write unit tests for Zustand store
    - Test hydrate loads state from storage correctly
    - Test dismiss writes "dismissed" to storage and hides panel
    - Test completeStep updates individual step and persists
    - Test reset produces clean state with all steps false
    - Test status transitions to "completed" when all steps are true
    - Create test at `apps/web/__tests__/onboarding.test.ts`
    - _Requirements: 5.2, 5.3, 6.2, 6.3, 10.2_

- [x] 3. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement detection hook and i18n keys
  - [x] 4.1 Create useStepCompletion hook
    - Create `apps/web/lib/hooks/useStepCompletion.ts`
    - Query existing API clients for groups, people/members, tasks, constraints, schedule-runs
    - Return `{ steps: StepCompletionMap, isLoading: boolean, refresh: () => void }`
    - Run detection when panel becomes visible or on explicit refresh call
    - Handle API failures gracefully — step remains incomplete, no error shown to user
    - Use `computeStepCompletion` from decisions module to map API responses to step booleans
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 10.3_

  - [x] 4.2 Add i18n translation keys for all three locales
    - Add `onboarding` namespace to `apps/web/messages/en.json` with keys for: steps (title, description, cta for each of 5 steps), panel title, success message, dismiss button, restart label
    - Add equivalent keys to `apps/web/messages/he.json` (Hebrew translations)
    - Add equivalent keys to `apps/web/messages/ru.json` (Russian translations)
    - _Requirements: 7.1, 7.3_

- [x] 5. Implement UI components
  - [x] 5.1 Create OnboardingPanel component
    - Create `apps/web/components/onboarding/OnboardingPanel.tsx`
    - Render as a floating panel at `inset-inline-end` using logical CSS properties
    - Display step list with completion indicators (checkmarks for done, highlight for current)
    - Show Step_CTA button for the current active step
    - Show dismiss button always visible when panel is open
    - Show success state with congratulatory message when all steps complete
    - Use progressive disclosure — detailed info only for active step
    - Use smooth transitions for state changes (Tailwind transition utilities)
    - Apply max-width constraint so panel does not cover full viewport
    - Use semantic HTML and ARIA attributes (role="dialog", aria-label, aria-live for step updates)
    - Implement focus trap within the panel
    - Support keyboard navigation for all interactive elements
    - Ensure 4.5:1 color contrast ratio using existing Tailwind palette
    - Use `useTranslations('onboarding')` from next-intl for all text
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 5.1, 7.1, 7.2, 7.4, 8.1, 8.2, 8.3, 8.4, 8.5, 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 5.2 Create OnboardingProvider component
    - Create `apps/web/components/onboarding/OnboardingProvider.tsx`
    - Wrap children and manage onboarding lifecycle
    - On mount: hydrate store from localStorage using current userId
    - Auto-show wizard when `shouldShowOnboarding` returns true (check groupCount and storage state)
    - Re-evaluate step completion via `useStepCompletion` when panel becomes visible
    - Gate rendering on userId being present (no onboarding for unauthenticated users)
    - Skip step detection when spaceId is null
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 4.6, 6.4_

  - [ ]* 5.3 Write unit tests for OnboardingPanel
    - Test step order renders correctly
    - Test success state renders when all steps complete
    - Test dismiss button is always visible
    - Test ARIA attributes are present on panel and interactive elements
    - Test RTL layout applied when locale is "he"
    - Test all three locales render without errors
    - _Requirements: 2.1, 2.3, 2.5, 5.1, 7.2, 7.3, 8.2_

- [x] 6. Integrate into AppShell and add restart menu item
  - [x] 6.1 Integrate OnboardingProvider into AppShell
    - Modify `apps/web/components/shell/AppShell.tsx`
    - Wrap main content area with `OnboardingProvider`
    - Render `OnboardingPanel` as a sibling to the main content (not inside it)
    - Ensure panel does not interfere with existing layout
    - _Requirements: 1.1, 9.4_

  - [x] 6.2 Add restart onboarding menu item to AppShell
    - Add a help/onboarding menu item in the AppShell navigation or help menu
    - On click: call `reset(userId)` on the onboarding store, then `show()`
    - Re-evaluate step completion after restart using current application state
    - Add i18n key for the menu item label in all three locales
    - _Requirements: 10.1, 10.2, 10.3_

  - [ ]* 6.3 Write integration tests for AppShell integration
    - Test OnboardingProvider renders within AppShell
    - Test restart menu item triggers store reset and re-evaluation
    - Test panel does not render when user is not authenticated
    - _Requirements: 1.1, 10.1, 10.2_

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using fast-check
- Unit tests validate specific examples and edge cases using vitest
- The project already has `fast-check@^3.23.2` as a dev dependency
- All file paths are relative to the monorepo root (`apps/web/...`)
- Existing Zustand stores in `lib/store/` provide the pattern to follow
- Existing hooks in `lib/hooks/` provide the pattern for the detection hook

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "2.1"] },
    { "id": 1, "tasks": ["1.3", "2.2", "4.2"] },
    { "id": 2, "tasks": ["2.3", "4.1"] },
    { "id": 3, "tasks": ["5.1", "5.2"] },
    { "id": 4, "tasks": ["5.3", "6.1", "6.2"] },
    { "id": 5, "tasks": ["6.3"] }
  ]
}
```
