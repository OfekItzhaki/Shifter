# 450 — Integrate RecommendationCard into HomeLeaveConfigPanel

## Phase

Feature: Recommendation Approval Flow (Informational Model)

## Purpose

Renders the passive `RecommendationCard` above the `EmergencyFreezeBanner` in the `HomeLeaveConfigPanel`, making double-shift recommendations visible to admins in the operational alerts area of the group settings tab.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/home-leave/HomeLeaveConfigPanel.tsx` | Added import for `RecommendationCard` and rendered it above `EmergencyFreezeBanner`, passing `spaceId` and `groupId` props |

## Key decisions

- Placed the card above the emergency freeze banner since the design specifies it should appear above the "operational alerts" area.
- The card self-manages its visibility (renders nothing when no active recommendations exist), so no conditional wrapper is needed in the panel.
- Props `spaceId` and `groupId` are already available in the panel's props — no additional data fetching required.

## How it connects

- Depends on `RecommendationCard` component (task 4.1) which fetches recommendations via `useRecommendations` hook.
- The `HomeLeaveConfigPanel` is only rendered when `isClosedBase` is true, matching the design requirement that the card is only visible in that context.
- The card's "Go to Tasks" button navigates to `?tab=tasks` within the same group page.

## How to run / verify

1. Navigate to a group page where `isClosedBase` is true
2. The `RecommendationCard` should appear above the emergency freeze banner when active recommendations exist
3. When no recommendations exist, nothing extra renders

## What comes next

- Task 4.3: Property test for RecommendationCard rendering
- Task 4.4: Unit tests for RecommendationCard

## Git commit

```bash
git add -A && git commit -m "feat(recommendations): integrate RecommendationCard into HomeLeaveConfigPanel"
```
