# 454 — Wire taskConfigurations to ScheduleTable2D

## Phase

Feature: Recommendation Approval Flow — Integration Wiring

## Purpose

The `ScheduleTable2D` component already accepts an optional `taskConfigurations` prop (added in task 6.3) and renders `TaskInfoBadge` next to each task name. However, no page was actually passing this data. This step wires the schedule data fetching so that `taskConfigurations` flows from the backend through to the component.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Scheduling/Queries/GetScheduleVersionsQuery.cs` | Extended `ScheduleVersionDetailDto` to include an optional `TaskConfigurations` dictionary. Updated the `GetScheduleVersionDetailQueryHandler` to always load group tasks and build the configurations map keyed by task name. Added `using Jobuler.Application.Scheduling.Models` import. |
| `apps/api/Jobuler.Application/Groups/Queries/GetGroupScheduleQuery.cs` | Changed `taskConfigurations` dictionary key from task ID to task name, matching what `ScheduleTable2D` expects for lookup. |
| `apps/web/lib/api/schedule.ts` | Added `taskConfigurations` optional field to `ScheduleVersionDetailDto` interface. Added import for `TaskConfigSummaryDto` from groups API. |
| `apps/web/app/admin/schedule/page.tsx` | Passed `taskConfigurations={selected.taskConfigurations}` to `ScheduleTable2D` in the `VersionDetailPanel`. |

## Key decisions

- **Keyed by task name, not ID**: `ScheduleTable2D` looks up configurations using `taskConfigurations?.[taskName]` where `taskName` comes from assignment's `taskTypeName`. Both the version detail and group schedule endpoints now key by task name for consistency.
- **Always load group tasks in version detail handler**: Previously, group tasks were only loaded when there were "missing" slot IDs (non-legacy tasks). Now they're always loaded to populate `taskConfigurations`. This adds a small query but ensures info badges always work.
- **DistinctBy for duplicate names**: Since the version detail endpoint is space-scoped (not group-scoped), there could be duplicate task names across groups. `DistinctBy` ensures no dictionary key collision.
- **Optional field with default null**: The `TaskConfigurations` parameter on `ScheduleVersionDetailDto` defaults to null, maintaining backward compatibility with any code that constructs the DTO without it.

## How it connects

- **Depends on**: Task 6.3 (TaskInfoBadge integration into ScheduleTable2D), Task 1.3 (TaskConfigSummaryDto), Task 3.3 (frontend type updates)
- **Enables**: Task info badges now display in the admin schedule page when viewing any version
- **Requirements**: 7.1 (task config as part of existing fetch), 7.2 (frontend has access to config fields)

## How to run / verify

1. Build the backend: `dotnet build` in `apps/api/Jobuler.Api`
2. Check frontend types: `npx tsc --noEmit` in `apps/web`
3. Navigate to the admin schedule page — task info badges should appear next to task names in the 2D grid
4. Hover/click a badge to see the task configuration popover

## What comes next

- Task 8.3: Property test for dismiss preserving task state
- Task 8.4: Integration tests

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval): wire taskConfigurations to ScheduleTable2D"
```
