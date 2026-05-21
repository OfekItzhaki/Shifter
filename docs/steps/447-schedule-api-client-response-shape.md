# 447 — Schedule API Client Response Shape Update

## Phase

Feature: Recommendation Approval Flow (Task 3.3)

## Purpose

The backend now returns a structured `GroupScheduleResponseDto` from the group schedule endpoint instead of a flat array of assignments. This step updates the frontend API client and all consumers to handle the new response shape, which includes both `assignments` and `taskConfigurations`.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/api/groups.ts` | Added `TaskConfigSummaryDto` and `GroupScheduleResponseDto` interfaces. Updated `getGroupSchedule` to return `GroupScheduleResponseDto` with backward-compatible handling for legacy flat arrays. |
| `apps/web/lib/query/hooks/useGroupSchedule.ts` | Updated return type to `GroupScheduleResponseDto`. Imported the new type. |
| `apps/web/app/schedule/today/page.tsx` | Updated to destructure `scheduleResponse?.assignments` instead of using the response directly as an array. |
| `apps/web/app/schedule/tomorrow/page.tsx` | Same destructuring update as today page. |
| `apps/web/app/groups/[groupId]/page.tsx` | Updated both direct `getGroupSchedule` call sites to extract `.assignments` from the response object. |

## Key decisions

- **Backward compatibility**: The `getGroupSchedule` function detects whether the response is a flat array (old format) or an object (new format) and normalizes to `GroupScheduleResponseDto` in both cases. This allows a graceful rollout where the frontend can work with both old and new backend versions.
- **Types live in `lib/api/groups.ts`**: Since `getGroupSchedule` already lives there alongside `GroupScheduleAssignmentDto`, the new DTOs are co-located rather than creating a separate file.
- **Hook exposes full response**: The `useGroupSchedule` hook returns the full `GroupScheduleResponseDto` so consumers can access both `assignments` and `taskConfigurations` as needed (the latter will be used by `TaskInfoBadge` in task 6.3/8.2).

## How it connects

- **Depends on**: Task 1.3 (backend `GetGroupScheduleQuery` now returns the new DTO shape)
- **Enables**: Task 6.3 (integrate `TaskInfoBadge` into `ScheduleTable2D` using `taskConfigurations`) and Task 8.2 (wire `taskConfigurations` prop to `ScheduleTable2D`)
- **Requirements**: 7.1 (task config data as part of existing schedule fetch), 7.2 (frontend has access to task config fields)

## How to run / verify

1. TypeScript compilation passes: `cd apps/web && npx tsc --noEmit`
2. The today/tomorrow pages and group page continue to render schedule data correctly
3. When the backend returns the new shape, `taskConfigurations` is available via the hook

## What comes next

- Task 8.2: Wire `taskConfigurations` from the schedule response into `ScheduleTable2D` as a prop
- Task 6.3: Integrate `TaskInfoBadge` into schedule column headers using the config data

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval-flow): update schedule API client for new response shape"
```
