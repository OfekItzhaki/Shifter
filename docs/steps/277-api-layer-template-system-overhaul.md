# 277 — API Layer Template System Overhaul

## Phase

Template System Overhaul — API layer changes (Tasks 7.1, 7.2, 7.3)

## Purpose

Expose the `TemplateType` property through the Groups API, add FluentValidation for the new `max_task_type_per_period` constraint type, and verify that dead `DislikedHatedScore` fields are fully removed from stats response DTOs. Also adds `task_type_counts` to cumulative stats responses.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs` | Added EF Core mapping for `TemplateType` → `template_type` column with string conversion and default `Custom`. |
| `Jobuler.Application/Groups/Queries/GetGroupsQuery.cs` | Added `TemplateType` field to `GroupDto` record. Updated handler to pass `g.TemplateType.ToString()`. |
| `Jobuler.Api/Controllers/GroupsController.cs` | Added `TemplateType` to `CreateGroupRequest` and `UpdateGroupRequest`. Updated `CreateGroup` to parse and pass template type. Updated `UpdateGroup` to call `SetGroupTemplateTypeCommand`. |
| `Jobuler.Application/Groups/Commands/CreateGroupCommand.cs` | Added `TemplateType` parameter to command record. Updated handler to pass it to `Group.Create()`. |
| `Jobuler.Application/Groups/Commands/SetGroupTemplateTypeCommand.cs` | New command + handler for updating a group's template type. |
| `Jobuler.Application/Groups/Validators/GroupCommandValidators.cs` | Added `.IsInEnum()` validation for `TemplateType` on `CreateGroupCommand`. |
| `Jobuler.Application/Constraints/Validators/CreateConstraintCommandValidator.cs` | Added conditional validation for `max_task_type_per_period` rule type: validates `task_type_name` (non-empty string), `max` (int > 0), `period_days` (int > 0). |
| `Jobuler.Application/Scheduling/Queries/GetCumulativeStatsQuery.cs` | Added `TaskTypeCounts` field to `CumulativePersonStatsDto`. Updated handler to deserialize and include task-type counts per time window. |

## Key decisions

- `TemplateType` is stored as text (string conversion) matching the `HomeLeaveMode` pattern — avoids PostgreSQL enum migration pain.
- Validation for `max_task_type_per_period` payload is done in the `CreateConstraintCommandValidator` using conditional `When()` — only triggers when the rule type matches.
- `DislikedHatedScore` was already fully removed from C# stats DTOs by task 1.3/1.4. Task 7.3 confirmed zero remaining references in `.cs` files.
- `task_type_counts` in cumulative stats is extracted from the JSONB field per the requested time window.

## How it connects

- The `GroupDto.TemplateType` field is consumed by the frontend feature visibility map (task 9.4).
- The `max_task_type_per_period` validation ensures the solver receives well-formed constraint payloads (task 5.2).
- The `SetGroupTemplateTypeCommand` is used by the update group endpoint and will be used by the template creation flow (task 11.1).

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore -v q
# Should compile with zero errors
```

## What comes next

- Frontend feature visibility map (task 9.1)
- Template seed data cleanup (task 9.2)
- End-to-end wiring of template type through group creation (task 11.1)

## Git commit

```bash
git add -A && git commit -m "feat(template-system): API layer - expose templateType, validate max_task_type_per_period, add task_type_counts to stats"
```
