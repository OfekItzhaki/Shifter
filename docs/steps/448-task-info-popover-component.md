# 448 — TaskInfoPopover Component

## Phase

Recommendation Approval Flow — Task Info Badge & Popover (Task 6.1)

## Purpose

Provides a popover component that displays task configuration details when an admin interacts with the task info badge in the schedule grid. This gives admins visibility into solver-relevant settings without navigating away from the schedule view.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/schedule/TaskInfoPopover.tsx` | New component that renders a positioned popover showing task configuration fields (double shift, overlap, time window, burden, qualifications, split count) with localized labels. Handles click-outside and blur dismissal. Shows "default settings" message when all values are at defaults. |

## Key decisions

- **No external popover library** — The project doesn't use Radix UI or any headless popover library. Followed the existing pattern from `NotificationBell.tsx` using a ref-based click-outside listener with `mousedown` events.
- **Absolute positioning** — The popover is positioned absolutely relative to its parent (the badge button), centered horizontally. This avoids the complexity of portal-based positioning for a small, inline tooltip.
- **`isDefaultConfig` helper** — Extracted a pure function to determine if all config values are at their defaults, making it easy to test and reuse.
- **Conditional rendering** — Split count only shown when > 1, qualifications only shown when non-empty, matching the requirements exactly.
- **`role="tooltip"`** — Used for accessibility, consistent with the project's existing tooltip patterns.

## How it connects

- Consumed by `TaskInfoBadge` (task 6.2) which controls open/close state
- Uses `TaskConfigSummaryDto` from `lib/api/groups.ts` (created in task 3.3)
- Uses localization keys from `messages/*.json` (created in task 7.1)
- Will be rendered inside `ScheduleTable2D` column headers (task 6.3)

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # should show no errors in TaskInfoPopover.tsx
```

The component will be visually testable once `TaskInfoBadge` (task 6.2) is implemented and wired into the schedule grid.

## What comes next

- Task 6.2: `TaskInfoBadge` component that triggers this popover
- Task 6.5: Property test for popover configuration display
- Task 6.6: Unit tests for popover behavior

## Git commit

```bash
git add -A && git commit -m "feat(schedule): add TaskInfoPopover component for task config display"
```
