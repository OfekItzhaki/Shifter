# 402 — Recommendation Banner Integration

## Phase
Feature: Double-Shift Recommendation

## Purpose
Integrates the `RecommendationBanner` component into the two primary views where admins review solver results: the `DraftScheduleModal` (full-screen draft review) and the `ScheduleTab` (schedule results view on the group page). This ensures admins see double-shift recommendations in context immediately after a solver run completes.

## What was built

### Modified files

- **`apps/web/app/groups/[groupId]/useGroupPageState.ts`** — Extended the `draftVersion` state type to include `sourceRunId?: string | null`, which maps to the backend's `ScheduleVersionDto.SourceRunId`.

- **`apps/web/app/groups/[groupId]/page.tsx`** — Updated the API response type annotations for draft version fetches (both initial load and post-solver-completion) to include `sourceRunId`. Passes `sourceRunId` to `DraftScheduleModal`.

- **`apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx`** — Added `sourceRunId` to the `DraftVersion` interface. Imported and rendered `RecommendationBanner` between the draft banner and the search filter, conditionally shown only when `isAdmin`, `spaceId`, and `draftVersion.sourceRunId` are all available.

- **`apps/web/components/DraftScheduleModal.tsx`** — Added `sourceRunId` prop. Imported and rendered `RecommendationBanner` at the top of the modal body content (before the view toggle), conditionally shown only when `sourceRunId` is present.

## Key decisions

1. **Conditional rendering handled by the banner itself** — The `RecommendationBanner` component already returns `null` when no recommendations exist. The integration only gates on whether a `sourceRunId` is available (meaning a solver run produced this draft).

2. **Admin-only visibility** — The banner is only rendered when `isAdmin` is true in the `ScheduleTab`, matching requirement 6.1. In the `DraftScheduleModal`, the modal itself is only shown to admins.

3. **sourceRunId from ScheduleVersion** — The backend's `ScheduleVersionDto` already includes `SourceRunId` linking the draft version to its solver run. This avoids needing a separate API call or state management for the run ID.

## How it connects

- Depends on task 13.1 (`RecommendationBanner` component) and task 12.2 (React Query hooks for recommendation API).
- The banner uses `useRecommendationsForRun` hook which calls `GET /spaces/{spaceId}/runs/{runId}/recommendations`.
- Satisfies Requirement 3.2: "WHEN the Admin views the solver results page and recommendations exist for that run, THE System SHALL display an inline banner."

## How to run / verify

1. Run the solver for a group that has staffing shortfalls.
2. After the solver completes, open the Schedule tab — the recommendation banner should appear below the draft banner.
3. Click "View Draft" to open the `DraftScheduleModal` — the recommendation banner should appear at the top of the modal content.
4. If no recommendations exist for the run, the banner should not render (no empty space).

## What comes next

- Task 14.2: Integrate `TaskDoubleShiftSuggestion` into group task settings.
- Task 15.1/15.2: Accept and dismiss recommendation flows.

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): integrate RecommendationBanner into draft modal and schedule tab"
```
