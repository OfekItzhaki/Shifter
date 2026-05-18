# Step 342 — Feedback FAB Component

## Phase
Feature — Feedback & Bug Report FAB (Frontend)

## Purpose
Provides a globally visible floating action button split into two halves (bug report and feedback) so users can quickly access the submission form from any authenticated page.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/shell/FeedbackFab.tsx` | Client component rendering a fixed-position split FAB with bug (left) and feedback (right) halves. Manages `modalOpen` and `submissionType` state, stores a ref to the clicked button for focus restoration. |
| `apps/web/components/shell/SubmissionModal.tsx` | Placeholder/stub component for the submission modal (full implementation in task 3.2). Accepts `open`, `submissionType`, `onClose`, and `triggerRef` props. |

## Key decisions

- **Inline SVGs** — Consistent with the rest of the project (no external icon library).
- **Tailwind CSS** — Used for styling with `min-w-[44px] min-h-[44px]` to meet the 44×44px touch target requirement.
- **Focus restoration via ref** — A mutable `triggerRef` is updated on each click to track which button opened the modal, enabling correct focus return on close.
- **Stub SubmissionModal** — Created a minimal placeholder so the FeedbackFab compiles without errors; task 3.2 will replace it with the full implementation.

## How it connects
- Will be rendered globally in the authenticated layout (task 3.4).
- Opens the SubmissionModal (task 3.2) which uses the useFeedbackSubmission hook (task 3.3) to POST to the backend FeedbackController (task 1.3).

## How to run / verify
- Import `<FeedbackFab />` in any page or layout and confirm it renders at bottom-left with correct positioning.
- Verify both halves are keyboard-focusable and have correct aria-labels.
- Click either half to confirm modal state updates.

## What comes next
- Task 3.2: Full SubmissionModal implementation with form, focus trap, and submission logic.
- Task 3.3: useFeedbackSubmission hook for API communication.
- Task 3.4: Wire FeedbackFab into the global authenticated layout.

## Git commit

```bash
git add -A && git commit -m "feat(feedback): add FeedbackFab split button component"
```
