# 327 — Command SplitCount Passthrough

## Phase

Split-Burden Scaling — API Layer Updates

## Purpose

Add `SplitCount` to the `CreateGroupTaskCommand` and `UpdateGroupTaskCommand` records so that the API layer can accept and forward the split count value to the domain entity's `Create`/`Update` methods. This enables callers (controllers, AI import, tests) to specify how many sub-shifts a task is divided into.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Tasks/Commands/GroupTaskCommands.cs` | Added `int SplitCount = 1` parameter to both `CreateGroupTaskCommand` and `UpdateGroupTaskCommand` records. Updated both handlers to pass `req.SplitCount` to the domain entity methods. |

## Key decisions

- Used a default value of `1` so all existing callers (controller, AI import, tests) continue to work without modification — backward compatible.
- No validation rule added here for `SplitCount >= 1` — that's handled in task 3.2 (FluentValidation) and the domain entity itself already throws on invalid values.
- The parameter is placed last in the record to maintain positional compatibility with existing callers.

## How it connects

- **Upstream**: The `TasksController` and `SmartImportCommand` create these commands. They default to `SplitCount = 1` until task 3.2/6.3 wire the request DTOs.
- **Downstream**: The `GroupTask.Create` and `GroupTask.Update` domain methods (task 1.3) already accept `int splitCount = 1` and persist it.
- **Next**: Task 3.2 adds `SplitCount` to the request DTOs and FluentValidation rules.

## How to run / verify

```bash
cd apps/api
dotnet build
dotnet test
```

Build succeeds with no new warnings. All existing tests pass since the new parameter defaults to 1.

## What comes next

- Task 3.2: Add `SplitCount` to request DTOs (`CreateGroupTaskRequest`, `UpdateGroupTaskRequest`) and FluentValidation rules.
- Task 6.3: Frontend sends `splitCount` in API requests.

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): add SplitCount to CreateGroupTaskCommand and UpdateGroupTaskCommand"
```
