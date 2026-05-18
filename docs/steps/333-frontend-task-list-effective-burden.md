# 333 — Frontend Task List: Display Effective Burden

## Phase

Split-Burden Scaling — Frontend Updates

## Purpose

When a task is split into multiple sub-shifts, the effective burden level is lower than the original. The task configuration view should visually communicate both levels so admins understand the impact of splitting on burden tracking.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/api/tasks.ts` | Added `effectiveBurdenLevel: string` and `splitCount: number` fields to `GroupTaskDto` |
| `apps/web/app/groups/[groupId]/tabs/TasksTab.tsx` | Updated task list to show "Original → Effective" burden badges when `splitCount > 1` and effective differs from original |
| `apps/web/messages/en.json` | Added `effectiveBurden` translation key |
| `apps/web/messages/he.json` | Added `effectiveBurden` translation key (Hebrew) |
| `apps/web/messages/ru.json` | Added `effectiveBurden` translation key (Russian) |

## Key decisions

- **Arrow indicator (→)**: When `splitCount > 1` and the effective burden differs from the original, both levels are shown side-by-side with an arrow: `קשה → רגיל`. This is compact and immediately communicates the reduction.
- **Fallback to single badge**: When `splitCount == 1` or effective equals original (e.g., short tasks below the 240-minute threshold), only the original burden badge is shown — no visual change from before.
- **No tooltip needed**: The arrow notation is self-explanatory in context. The SubShiftEditor already shows the split count.
- **Defensive check**: The condition checks `task.effectiveBurdenLevel` exists (truthy) before comparing, ensuring backward compatibility if the API hasn't been updated yet.

## How it connects

- Depends on API task 3.3 (`GroupTaskResponseDto` returning `EffectiveBurdenLevel` and `SplitCount`)
- The `burdenLabels` and `burdenColors` maps from `types.ts` handle both cased and lowercase burden level strings
- The Sandbox tasks tab uses a different DTO (solver input) and is not affected

## How to run / verify

1. Create a group task with a long duration (e.g., 12 hours) and burden level "Hard"
2. Split it into 2 sub-shifts via the SubShiftEditor
3. Save the task
4. In the task list, verify the burden badge shows: `קשה → רגיל` (Hard → Normal)
5. Create a task with `splitCount = 1` — verify only the single burden badge shows

## What comes next

- Task 6.3: Update SubShiftEditor to send `splitCount` in API requests
- Task 6.4: Verify schedule grid and statistics pages use snapshot burden level

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): display effective burden in task list when split"
```
