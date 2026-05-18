# Step 343 — Submission Modal Component

## Phase
Feature: Feedback & Bug Report FAB (Frontend)

## Purpose
Implements the full `SubmissionModal` component that replaces the placeholder created in task 3.1. This modal provides the user-facing form for submitting bug reports and feedback, with full accessibility support including focus trapping, keyboard navigation, and screen reader announcements.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/shell/SubmissionModal.tsx` | Full client component replacing the placeholder — includes textarea with 5000 char limit, character counter, loading/success/error/rate-limit states, focus trap, escape-to-close, backdrop dismiss, and focus restoration |

## Key decisions

- **Used `useFeedbackSubmission` hook directly** — the hook already handles all API interaction, error parsing, and rate-limit header extraction, so the modal only manages UI state.
- **Inline focus trap** — implemented a simple focus trap matching the pattern used in `ReAuthDialog.tsx` rather than pulling in a third-party library.
- **Auto-close on success** — uses `setTimeout(2000)` with cleanup on unmount to prevent memory leaks.
- **Tailwind CSS styling** — consistent with the project's existing component styling (dark mode support, rounded corners, ring focus indicators).
- **Portal-free approach** — the modal uses `fixed inset-0 z-[1100]` positioning (above the FAB's z-1000) rather than a React portal, matching the pattern in other dialogs.

## How it connects

- Consumed by `FeedbackFab.tsx` which passes `open`, `submissionType`, `onClose`, and `triggerRef` props.
- Uses `useFeedbackSubmission` hook from `apps/web/hooks/useFeedbackSubmission.ts` for API submission.
- The hook calls `POST /feedback` which is handled by `FeedbackController` on the backend.

## How to run / verify

1. Start the dev server: `npm run dev` in the web app
2. Navigate to any authenticated page
3. Click either half of the FAB (bottom-left corner)
4. Verify the modal opens with correct title ("Bug Report" or "Feedback")
5. Test: character counter updates as you type
6. Test: submit button is disabled when textarea is empty
7. Test: pressing Escape closes the modal
8. Test: clicking the backdrop closes the modal
9. Test: Tab/Shift+Tab cycles within the modal
10. Test: focus returns to the FAB button that triggered the modal on close

## What comes next

- Task 3.4: Wire FeedbackFab into the global layout
- Task 6.1/6.2: Frontend property-based tests for character count and whitespace rejection
- Task 7.2: Unit tests for SubmissionModal behavior

## Git commit

```bash
git add -A && git commit -m "feat(feedback): implement SubmissionModal with full a11y and state management"
```
