# 456 — RecommendationCard Unit Tests

## Phase

Feature: Recommendation Approval Flow — Task 4.4

## Purpose

Validates the RecommendationCard component behavior through unit tests covering rendering, empty state, navigation, and dismiss functionality.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/recommendations/RecommendationCard.test.tsx` | Unit tests for RecommendationCard component |

## Key decisions

- Followed the existing mocking pattern from `freezeDeactivationDialog.test.tsx` — mocking hooks at module level with `vi.mock`
- Mocked `useRecommendations`, `useDismissRecommendation`, `useRouter`, and `useTranslations` to isolate component logic
- Used module-level mutable variables (`mockRecommendationsData`, `mockIsLoading`) to control hook return values per test
- Tested all four behaviors specified in the task: render with data, empty state, navigation, and dismiss

## How it connects

- Tests validate the `RecommendationCard` component created in task 4.1
- Covers requirements 2.1 (card renders), 2.4 (dismiss), 2.5 (empty state), and 3.1 (navigation)
- Complements the property test in task 4.3 which validates content correctness across arbitrary inputs

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/recommendations/RecommendationCard.test.tsx
```

All 9 tests should pass.

## What comes next

- Task 6.4: Property test for TaskInfoBadge presence and accessibility
- Task 6.5: Property test for TaskInfoPopover configuration display
- Task 6.6: Unit tests for TaskInfoBadge and TaskInfoPopover

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval-flow): add RecommendationCard unit tests"
```
