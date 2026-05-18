# 334 — Frontend SubShiftEditor Sends splitCount in API Requests

## Phase

Split-Burden Scaling — Export and Frontend Updates (Task 6.3)

## Purpose

The SubShiftEditor component previously only communicated the computed `shiftDurationMinutes` (original duration ÷ N) back to the parent form. The API now accepts a `splitCount` field on create/update task requests, so the frontend needs to track and send the number of sub-shifts selected by the admin. This enables the backend to persist `SplitCount` on the GroupTask entity and compute effective burden levels.

## What was built

- **`apps/web/lib/api/tasks.ts`** — Added `splitCount?: number` to `GroupTaskPayload` interface. Also added `effectiveBurdenLevel?: string` and `splitCount: number` to `GroupTaskDto` (already present from task 3.3, confirmed).
- **`apps/web/app/groups/[groupId]/tabs/TasksTab.tsx`** — Added `splitCount: number` to the `TaskForm` interface. Updated `SubShiftEditor` to accept `splitCount` and `onSplitCountChange` props, reporting the current number of sub-shifts back to the parent. When editing an existing task, the editor initializes from the persisted split count.
- **`apps/web/app/groups/[groupId]/useGroupPageState.ts`** — Added `splitCount: 1` to `DEFAULT_TASK_FORM`.
- **`apps/web/app/groups/[groupId]/page.tsx`** — Added `splitCount: taskForm.splitCount` to the API payload in `handleTaskSubmit`. Updated `onEditTask` handler to populate `splitCount: t.splitCount ?? 1` from the existing task DTO.

## Key decisions

- `splitCount` is optional in `GroupTaskPayload` (defaults to 1 on the API side) for backward compatibility with existing code paths like group templates.
- The `SubShiftEditor` now receives the initial split count as a prop so it can correctly initialize when editing an existing split task (computing `originalMinutes = totalMinutes * initialSplitCount`).
- The `onSplitCountChange` callback is separate from `onChange` (which handles duration) to keep concerns clear and avoid coupling the two values in a single update.

## How it connects

- Depends on: Task 3.2 (API accepts `SplitCount` in request DTOs with validation)
- Depends on: Task 3.3 (API returns `splitCount` and `effectiveBurdenLevel` in response DTO)
- Consumed by: Backend `GroupTask.Create`/`Update` which persists the split count
- Related: Task 6.2 (frontend displays effective burden) uses the same `splitCount` field from the DTO

## How to run / verify

1. Open the group detail page and create a new task with a duration > 60 minutes.
2. Use the sub-shift editor (+/−) to split the shift into multiple parts.
3. Submit the form and inspect the network request — the payload should include `splitCount` matching the number of sub-shifts.
4. Edit the task — the sub-shift editor should initialize with the correct split count from the API response.
5. Verify TypeScript compiles cleanly: `npx tsc --noEmit` in the web app.

## What comes next

- Task 6.4: Verify schedule grid and statistics pages use snapshot burden level
- Task 7: Final checkpoint — full integration verification

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): SubShiftEditor sends splitCount in API requests"
```
