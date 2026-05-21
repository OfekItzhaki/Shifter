# 448 — RecommendationCard Component

## Phase

Recommendation Approval Flow — Informational Card

## Purpose

Creates the passive informational `RecommendationCard` component that replaces the old action-oriented recommendation UI. The card displays which tasks have uncovered slots and provides navigation to the Tasks tab, along with a dismiss action.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/recommendations/RecommendationCard.tsx` | New component that fetches active recommendations, displays task names and uncovered slot counts, and provides "Go to Tasks" and "Dismiss" buttons |

## Key decisions

- **Blue color scheme** — Used blue (informational) instead of amber (warning) to differentiate from the existing `RecommendationBanner` which uses amber. The card is passive/informational, not urgent.
- **Dismiss all recommendations** — When the user clicks "Dismiss", all active recommendations are dismissed rather than requiring individual dismissal. This matches the card's aggregate display.
- **Render nothing pattern** — Returns `null` when loading or when no recommendations exist, consistent with `RecommendationBanner` and `TaskDoubleShiftSuggestion`.
- **Aggregate slot count** — Sums `totalUncoveredSlotsInRun` across all recommendations to show the total impact.
- **Responsive layout** — Uses flex-col on mobile, flex-row on sm+ breakpoints, matching the `RecommendationBanner` pattern.

## How it connects

- Uses `useRecommendations` and `useDismissRecommendation` hooks from `lib/query/hooks/useRecommendations.ts`
- Uses localization keys from `messages/en.json` under the `recommendations` namespace (added in step 446)
- Will be integrated into `HomeLeaveConfigPanel` in task 4.2
- Navigates to `?tab=tasks` using the same pattern as `RecommendationBanner`

## How to run / verify

```bash
# Type check
cd apps/web && npx tsc --noEmit
```

The component renders nothing until integrated into a parent (task 4.2). Visual verification requires the full integration.

## What comes next

- Task 4.2: Integrate `RecommendationCard` into `HomeLeaveConfigPanel`
- Task 4.3: Property test for card rendering
- Task 4.4: Unit tests for card behavior

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): create passive RecommendationCard component"
```
