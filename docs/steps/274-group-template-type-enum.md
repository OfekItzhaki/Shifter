# 274 — GroupTemplateType Enum and Group Entity Extension

## Phase

Template System Overhaul — Domain Layer

## Purpose

Introduce the `GroupTemplateType` enum and persist it on the `Group` entity so the platform can distinguish between different group types (Army, Restaurant, Hospital, Security, Custom) and drive feature visibility from the template type.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Groups/GroupTemplateType.cs` | New enum with values: Army, Restaurant, Hospital, Security, Custom |
| `apps/api/Jobuler.Domain/Groups/Group.cs` | Added `TemplateType` property (default `Custom`), `SetTemplateType()` method, and optional `templateType` parameter to `Group.Create()` |

## Key decisions

- Followed the same pattern as `HomeLeaveMode` enum (simple enum in its own file, same namespace)
- Default is `Custom` so existing groups retain full functionality without migration issues
- The `templateType` parameter in `Group.Create()` is optional with default `Custom` — no breaking changes to existing callers
- `SetTemplateType` calls `Touch()` to update audit timestamps, consistent with other setters

## How it connects

- The database migration (task 1.1, step 273) adds the `template_type TEXT` column
- The EF Core configuration (task 3.3) will map this property to the column
- The API layer (task 7.1) will expose `templateType` in DTOs
- The frontend feature visibility map (task 9.1) uses the template type to show/hide features

## How to run / verify

```bash
cd apps/api
dotnet build
```

All projects compile successfully with no new warnings.

## What comes next

- Task 1.3: Remove dead fields from `CumulativeRecord` and `AssignmentCountsDelta`
- Task 3.3: EF Core configuration to map `TemplateType` to the `template_type` column

## Git commit

```bash
git add -A && git commit -m "feat(template-system): add GroupTemplateType enum and extend Group entity"
```
