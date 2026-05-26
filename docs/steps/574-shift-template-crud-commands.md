# 574 — Shift Template CRUD Commands

## Phase
Self-Service Scheduling — Application Layer

## Purpose
Implements the Create, Update, and Delete (soft-delete) commands for ShiftTemplates, along with FluentValidation validators and list/get queries. This enables group admins to manage recurring weekly shift patterns that drive slot generation.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/Commands/ShiftTemplateCommands.cs` | CreateShiftTemplateCommand, UpdateShiftTemplateCommand, DeleteShiftTemplateCommand with validators and handlers |
| `apps/api/Jobuler.Application/Scheduling/SelfService/Queries/ShiftTemplateQueries.cs` | GetShiftTemplateQuery and ListShiftTemplatesQuery with handlers |
| `apps/api/Jobuler.Domain/Scheduling/ShiftSlot.cs` | Added `UpdateFromTemplate` method for cascading template changes to unprotected slots |
| `apps/api/Jobuler.Tests/Validation/ShiftTemplateCommandValidatorTests.cs` | 32 unit tests covering all validator rules for Create, Update, and Delete commands |

## Key decisions

1. **Single file for CRUD commands** — Following the existing `GroupTaskCommands.cs` pattern, all three commands (Create, Update, Delete) live in one file for cohesion.
2. **Soft-delete preserves all existing slots** — When a template is deleted, existing slots remain untouched regardless of request status. Only future slot generation is affected.
3. **Update cascades to unprotected future slots** — When a template is updated, future slots that have zero approved requests are updated to match the new template values. Slots with at least one approved request are preserved unchanged (Requirement 2.4).
4. **Permission: TasksManage** — Template management uses the same permission as GroupTask management, since templates are closely related to tasks.
5. **UpdateFromTemplate on ShiftSlot** — Added a domain method to cleanly update slot properties from a modified template, keeping the logic encapsulated.

## How it connects

- **Domain layer**: Uses `ShiftTemplate.Create()`, `Update()`, and `SoftDelete()` methods already defined in task 1.4
- **Infrastructure layer**: Uses EF Core configurations and migrations from task 2.1/2.2
- **Downstream**: The `SlotGenerationService` (task 5.3) will use these templates to generate slots
- **API layer**: The `ShiftTemplatesController` (task 14.2) will dispatch these commands

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --filter "FullyQualifiedName~ShiftTemplateCommandValidatorTests" --no-build
```

All 32 validator tests should pass.

## What comes next

- Task 5.2: Property tests for shift template validation (Property 4 and Property 8)
- Task 5.3: SlotGenerationService implementation
- Task 14.2: ShiftTemplatesController API endpoints

## Git commit

```bash
git add -A && git commit -m "feat(self-service): shift template CRUD commands and validators"
```
