# 515 — TriggerRegenerationValidator

## Phase

Phase: Schedule Regeneration — Application Layer

## Purpose

Adds FluentValidation input validation for the `TriggerRegenerationCommand`, ensuring all required Guid fields (`SpaceId`, `GroupId`, `RequestedByUserId`) are non-empty before the command handler executes. This prevents invalid requests from reaching business logic and satisfies the architecture rule that all commands require FluentValidation validators.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Commands/TriggerRegenerationCommand.cs` | Command record and `TriggerRegenerationValidator` with `.NotEmpty()` rules for all three Guid fields |

## Key decisions

- **Validator co-located with command**: Follows the same pattern as `TriggerSolverCommand.cs` and `DismissRecommendationCommand.cs` where the validator lives in the same file as the command record.
- **`.NotEmpty()` for Guid fields**: `NotEmpty()` on a `Guid` rejects both `Guid.Empty` and `default(Guid)`, which is the standard pattern used throughout the project.
- **Class name `TriggerRegenerationValidator`**: Matches the naming convention specified in the design document.

## How it connects

- The validator is auto-discovered by MediatR's FluentValidation pipeline behavior (registered in DI) and runs before the command handler.
- The command record will be used by the `TriggerRegenerationCommandHandler` (task 4.1) and dispatched from the API controller (task 5.1).
- Validates Requirement 1.3 — ensuring the regeneration dispatch has valid identifiers.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application
```

The validator is automatically picked up by the FluentValidation pipeline behavior registered in `Program.cs`.

## What comes next

- Task 4.1: Implement the `TriggerRegenerationCommandHandler` with subscription check, concurrency guard, and job dispatch logic.
- Task 4.3–4.5: Property-based tests for the command handler behavior.

## Git commit

```bash
git add -A && git commit -m "feat(scheduling): add TriggerRegenerationCommand record and FluentValidation validator"
```
