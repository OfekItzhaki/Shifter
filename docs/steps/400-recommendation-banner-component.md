# Step 400 — Recommendation Banner Component

## Phase
Phase 5 — Double-Shift Recommendation Frontend

## Purpose
Creates the `RecommendationBanner` component that displays an inline warning banner when the recommendation engine detects staffing shortfalls for a solver run. The banner surfaces actionable information to admins: total uncovered slots, affected task names, date range, and a CTA to navigate to task settings.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/recommendations/RecommendationBanner.tsx` | New component that fetches recommendation data via `useRecommendationsForRun` hook and renders a visually prominent amber banner with uncovered slot count, up to 5 task names (+N more indicator), affected date range, and a CTA button |
| `apps/web/messages/en.json` | Added `recommendations` translation section with `bannerTitle`, `andMore`, `affectedRange`, `viewTasks` keys |
| `apps/web/messages/he.json` | Added Hebrew translations for the recommendations section |
| `apps/web/messages/ru.json` | Added Russian translations for the recommendations section |

## Key decisions

1. **Props design** — Component accepts `spaceId`, `runId`, and `groupId` as props. The `runId` is needed for the API query, and `groupId` is needed for the CTA navigation.
2. **Conditional rendering** — Returns `null` when loading, when no data exists, or when recommendations array is empty. This ensures the banner only appears when relevant (Requirement 6).
3. **Amber color scheme** — Uses amber/warning colors (not red/error) to be visually prominent without being alarming. Matches the "info/warning style but not blocking" guidance.
4. **Date range fallback** — Uses `affectedDateRange` from the API response, with a client-side fallback that computes the range from individual recommendation dates if the API field is empty.
5. **Navigation pattern** — CTA navigates to `/groups/{groupId}?tab=tasks`. The group page manages tab state internally; the integration task (13.2) will handle reading the query param.
6. **Accessibility** — Uses `role="alert"` and `aria-live="polite"` for screen reader announcements.

## How it connects

- **Data source**: Uses `useRecommendationsForRun` hook (created in task 12.2) which calls `GET /spaces/{spaceId}/runs/{runId}/recommendations`
- **API types**: Uses `RecommendationBanner` interface from `lib/api/recommendations.ts` (created in task 12.1)
- **Integration points**: Will be rendered in `DraftScheduleModal` and `ScheduleTab` (task 13.2)
- **Navigation target**: Links to the group tasks tab where `TaskDoubleShiftSuggestion` (task 14.1) will show inline suggestions

## How to run / verify

1. The component compiles without TypeScript errors
2. When integrated (task 13.2), it will appear in the draft schedule modal and schedule tab when recommendations exist for the current solver run
3. When no recommendations exist, the component renders nothing (no empty state visible)

## What comes next

- Task 13.2: Integrate `RecommendationBanner` into `DraftScheduleModal` and `ScheduleTab`
- Task 14.1: Create `TaskDoubleShiftSuggestion` inline component for task settings

## Git commit

```bash
git add -A && git commit -m "feat(double-shift): add RecommendationBanner component with i18n"
```
