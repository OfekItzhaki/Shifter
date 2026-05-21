# 445 — TaskConfigSummaryDto and Extended Schedule Response

## Phase

Recommendation Approval Flow — Backend Simplification (Task 1.3)

## Purpose

The schedule grid needs task configuration data (double shift, overlap, time window, burden, qualifications, split count) to display info badges without additional API calls. This step extends the `GetGroupScheduleQuery` response to include a `TaskConfigurations` dictionary alongside the existing assignments list.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Models/TaskConfigSummaryDto.cs` | New lightweight DTO summarizing a GroupTask's configuration for frontend display |
| `apps/api/Jobuler.Application/Groups/Queries/GetGroupScheduleQuery.cs` | Modified: added `GroupScheduleResponseDto` wrapper, changed return type from `List<GroupScheduleAssignmentDto>` to `GroupScheduleResponseDto`, handler now loads all active GroupTasks and builds a task configurations dictionary |

## Key decisions

1. **TaskConfigSummaryDto placed in `Scheduling/Models/`** — Follows existing convention where scheduling-related DTOs live in this folder (alongside `RecommendationDto`, `SolverInputDto`, etc.)
2. **GroupScheduleResponseDto placed alongside the query** — Since it's the direct return type of the query, it lives in the same file for discoverability.
3. **GroupTasks always loaded** — Previously, GroupTasks were only loaded when `missingSlotIds` existed. Now they're always loaded since we need them for the task configurations dictionary regardless. This is a single query that was already happening conditionally.
4. **Task ID as string key** — The dictionary uses `Guid.ToString()` as the key, matching the `TaskId` field in the DTO. This simplifies JSON serialization and frontend consumption.
5. **TimeOnly formatted as "HH:mm"** — DailyStartTime/DailyEndTime are serialized as strings for cross-platform compatibility.
6. **BurdenLevel as string** — Uses `ToString()` on the enum to produce "Easy", "Normal", or "Hard".

## How it connects

- **Frontend (Task 3.3)**: The frontend schedule API client must be updated to handle the new response shape (`{ assignments, taskConfigurations }` instead of a flat array).
- **TaskInfoBadge (Task 6.2–6.3)**: The `taskConfigurations` dictionary is passed to `ScheduleTable2D` which renders info badges per task.
- **Cache**: The cache now stores the full `GroupScheduleResponseDto` object instead of just the assignments list.
- **Controller**: The `GroupsController.GetGroupSchedule` endpoint automatically returns the new shape since it dispatches the query via MediatR.

## How to run / verify

```bash
cd apps/api
dotnet build
dotnet test
```

The build should succeed. The endpoint `GET /spaces/{spaceId}/groups/{groupId}/schedule` now returns:
```json
{
  "assignments": [...],
  "taskConfigurations": {
    "<taskId>": {
      "taskId": "...",
      "allowsDoubleShift": false,
      "allowsOverlap": false,
      "dailyStartTime": "08:00",
      "dailyEndTime": "16:00",
      "burdenLevel": "Normal",
      "requiredQualificationNames": ["Medic"],
      "splitCount": 1
    }
  }
}
```

## What comes next

- Task 1.4: Unit tests for backend changes (verify taskConfigurations in response)
- Task 3.3: Frontend schedule API client update to handle new response shape
- Task 6.2–6.3: TaskInfoBadge integration using the taskConfigurations data

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval): add TaskConfigSummaryDto and extend schedule response"
```
