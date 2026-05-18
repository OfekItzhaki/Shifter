# 342 — useFeedbackSubmission Hook

## Phase
Feature: Feedback & Bug Report FAB — Frontend

## Purpose
Provides a React hook that encapsulates the feedback/bug report submission logic, including API communication with a 10-second timeout, rate-limit handling (429 + Retry-After header parsing), and graceful error handling for network failures and timeouts.

## What was built

| File | Description |
|------|-------------|
| `apps/web/hooks/useFeedbackSubmission.ts` | Custom hook exporting `submit`, `status`, `errorMessage`, `retryAfterSeconds`, and `reset` |

## Key decisions

- **Per-request timeout**: The 10-second timeout is set on the individual `apiClient.post` call via Axios config, not globally on the client instance.
- **Axios error code detection**: Timeouts are detected via `ECONNABORTED` or `ERR_CANCELED` with a timeout message, covering both Axios timeout modes.
- **Retry-After parsing**: The `retry-after` header (lowercase, as Axios normalizes headers) is parsed as an integer representing seconds.
- **No external state management**: Uses plain React `useState` — no Zustand or React Query needed for this fire-and-forget flow.
- **Stable callbacks**: `submit` and `reset` are wrapped in `useCallback` with empty deps to maintain referential stability.

## How it connects

- Consumed by `SubmissionModal` component (task 3.2) to drive form submission state.
- Uses the shared `apiClient` from `@/lib/api/client` which handles auth token injection and 401 refresh.
- The backend `FeedbackController` (task 1.3) returns 429 with `Retry-After` header that this hook parses.

## How to run / verify

```bash
# TypeScript compilation check
cd apps/web && npx tsc --noEmit
```

The hook will be exercised by the SubmissionModal component and tested via unit/property tests in later tasks.

## What comes next

- Task 3.4: Wire FeedbackFab into global layout (uses this hook indirectly via SubmissionModal)
- Task 6.1/6.2: Frontend property-based tests
- Task 7.2: Unit tests for SubmissionModal (which uses this hook)

## Git commit

```bash
git add -A && git commit -m "feat(feedback): add useFeedbackSubmission hook with timeout and rate-limit handling"
```
