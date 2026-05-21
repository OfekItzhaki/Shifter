# Step 449 — TaskInfoBadge Component

## Phase

Recommendation Approval Flow — Task info badge and popover in schedule grid

## Purpose

Provides a small, visually subtle "ℹ" icon button that admins can click to reveal task configuration details via the `TaskInfoPopover`. This badge is rendered next to each task name in the schedule grid column headers, giving admins quick access to task settings without navigating away.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/schedule/TaskInfoBadge.tsx` | New component — renders a small "ℹ" button that toggles `TaskInfoPopover` on click. Returns null when config is null/undefined. |

## Key decisions

- **Render nothing when config is unavailable** — satisfies Requirement 7.3 (graceful degradation when task config data is missing).
- **Relative positioning wrapper** — uses `relative inline-flex` so the absolutely-positioned `TaskInfoPopover` anchors correctly below the badge.
- **Visually subtle styling** — small 4×4 (w-4 h-4) button with muted slate-400 color, only darkening on hover. Does not distract from schedule data (Requirement 5.2).
- **Accessible** — includes `aria-label="Task configuration info"` (Requirement 5.3).
- **Click-to-toggle** — uses local `useState` to open/close the popover. The popover's own `onClose` callback handles click-outside dismissal.

## How it connects

- Consumed by `ScheduleTable2D` column headers (task 6.3) — each task header renders one `TaskInfoBadge`.
- Delegates display to `TaskInfoPopover` (task 6.1) which shows the full task configuration.
- Accepts `TaskConfigSummaryDto` from `lib/api/groups.ts` — the same type used throughout the schedule data flow.

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

The component renders correctly when:
1. `config` is null/undefined → nothing rendered
2. `config` is provided → "ℹ" button appears
3. Clicking the button → `TaskInfoPopover` opens
4. Clicking outside → popover closes

## What comes next

- Task 6.3: Integrate `TaskInfoBadge` into `ScheduleTable2D` column headers
- Task 6.4: Property test for badge presence and accessibility
- Task 6.6: Unit tests for badge and popover

## Git commit

```bash
git add -A && git commit -m "feat(schedule): add TaskInfoBadge component for task config visibility"
```
