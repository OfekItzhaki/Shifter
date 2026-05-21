# 457 — Dismiss Preserves Task State Property Test

## Phase

Recommendation Approval Flow — Property-Based Testing

## Purpose

Validates Property 1 from the design document: for any recommendation and any GroupTask state, dismissing the recommendation sets the recommendation status to "Dismissed" AND leaves the GroupTask's `AllowsDoubleShift` property unchanged. This ensures the dismiss operation never has side effects on task configuration.

## What was built

- `apps/web/__tests__/recommendations/dismissPreservesTaskState.property.test.ts` — Property-based test using fast-check that generates arbitrary recommendation and GroupTask state combinations and asserts the dismiss contract holds across all inputs.

## Key decisions

- Implemented as a pure logic test (no DOM rendering) that mirrors the backend dismiss operation's contract
- Used fast-check arbitraries to generate diverse recommendation and GroupTask states
- Minimum 100 iterations per property (actually 200 for the main properties)
- Four sub-properties tested: status change, full task preservation, dismiss metadata, and recommendation field preservation

## How it connects

- Validates Requirements 1.1, 1.3, 4.2 from the recommendation-approval-flow spec
- Complements the backend unit tests for the DismissRecommendationCommand
- Follows the same pattern as other property tests in the project (fairnessWarning, formatTime)

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/recommendations/dismissPreservesTaskState.property.test.ts
```

## What comes next

- Task 8.4: Integration tests for the recommendation approval flow

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval-flow): dismiss preserves task state property test"
```
