# 190 — Home-Leave Template CRUD Commands & Queries

## Phase

Phase: Home-Leave Scheduling — API Backend (Templates)

## Purpose

Implements the Application-layer CRUD operations for home-leave templates: create, delete, list, and load. Templates allow group admins to save and reuse standard home-leave configurations across closed-base groups within a space.

## What was built

| File | Description |
|------|-------------|
| `Application/HomeLeave/Commands/CreateHomeLeaveTemplateCommand.cs` | MediatR command + handler: validates name, checks duplicate (throws ConflictException → 409), persists template |
| `Application/HomeLeave/Commands/DeleteHomeLeaveTemplateCommand.cs` | MediatR command + handler: deletes template by ID, throws KeyNotFoundException → 404 if not found |
| `Application/HomeLeave/Queries/ListHomeLeaveTemplatesQuery.cs` | MediatR query + handler: lists all templates for a space, sorted by created_at descending |
| `Application/HomeLeave/Queries/LoadHomeLeaveTemplateQuery.cs` | MediatR query + handler: returns single template by ID, throws KeyNotFoundException → 404 if not found |
| `Application/HomeLeave/Validators/CreateHomeLeaveTemplateCommandValidator.cs` | FluentValidation: name (1–100 chars, no leading/trailing whitespace), config value ranges |
| `Application/HomeLeave/Validators/DeleteHomeLeaveTemplateCommandValidator.cs` | FluentValidation: SpaceId and TemplateId must be non-empty |

## Key decisions

- **ConflictException for duplicate names**: The project already has `ConflictException` (inherits `InvalidOperationException`) mapped to HTTP 409 in the middleware. Used this for duplicate template name detection.
- **Permission check in handler**: Both create and delete handlers call `IPermissionService.RequirePermissionAsync` with `Permissions.ConstraintsManage` before any DB operation.
- **Tenant isolation**: All queries filter by `SpaceId` to enforce tenant isolation at the application layer (in addition to RLS at the DB level).
- **Shared DTO**: `HomeLeaveTemplateDto` is defined in the queries namespace and reused by both `ListHomeLeaveTemplatesQuery` and `LoadHomeLeaveTemplateQuery`.
- **Name trimming in handler**: The create handler trims the name before persisting, while the validator ensures no leading/trailing whitespace was submitted (rejecting invalid input early).

## How it connects

- These commands/queries are dispatched by the `HomeLeaveTemplatesController` (task 4.2).
- The `HomeLeaveTemplate` domain entity (task 1.3) provides the `Create` factory method with built-in name validation.
- The `AppDbContext.HomeLeaveTemplates` DbSet (task 1.5) provides EF Core access.
- FluentValidation validators are auto-registered via the `ValidationBehavior` MediatR pipeline behavior.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
```

All projects compile cleanly with zero warnings or errors.

## What comes next

- Task 4.2: Create `HomeLeaveTemplatesController` to wire these commands/queries to HTTP endpoints.
- Task 16.2: Property-based tests for template name validation.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): template CRUD commands, queries, and validators"
```
