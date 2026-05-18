# 328 — Request DTO SplitCount & FluentValidation

## Phase

Split-Burden Scaling — API Layer

## Purpose

Add the `SplitCount` field to the API request DTOs (`CreateGroupTaskRequest` and `UpdateGroupTaskRequest`) and add FluentValidation rules to reject requests with `SplitCount < 1`. This ensures the API layer validates split count before it reaches the domain, and existing API consumers remain unaffected thanks to the default value of 1.

## What was built

- **`apps/api/Jobuler.Api/Controllers/TasksController.cs`**
  - Added `int SplitCount = 1` to `CreateGroupTaskRequest` record
  - Added `int SplitCount = 1` to `UpdateGroupTaskRequest` record
  - Updated `CreateGroupTask` action to pass `req.SplitCount` to the command
  - Updated `UpdateGroupTask` action to pass `req.SplitCount` to the command

- **`apps/api/Jobuler.Application/Tasks/Commands/GroupTaskCommands.cs`**
  - Added `RuleFor(x => x.SplitCount).GreaterThanOrEqualTo(1)` to `CreateGroupTaskCommandValidator`
  - Added `RuleFor(x => x.SplitCount).GreaterThanOrEqualTo(1)` to `UpdateGroupTaskCommandValidator`

## Key decisions

- Default value of `1` on the request DTOs ensures backward compatibility — existing clients that don't send `SplitCount` will get the default behavior (no split).
- Validation is at the command validator level (FluentValidation in Application layer) following the project's existing pattern, providing a 400 Bad Request before the domain entity is ever touched.
- The domain entity also validates `SplitCount >= 1` as a defense-in-depth measure.

## How it connects

- Depends on task 3.1 (command records already have `SplitCount`)
- Depends on task 1.3 (domain entity `GroupTask.Create`/`Update` accept `splitCount`)
- Enables task 3.3 (response DTO with effective burden) and task 6.3 (frontend sending `splitCount`)

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build should succeed with no new errors or warnings.

To verify validation rejects invalid input, send a request with `"splitCount": 0` — should return 400.

## What comes next

- Task 3.3: Update `GroupTaskResponseDto` to include effective burden and split count
- Task 6.3: Frontend SubShiftEditor sends `splitCount` in API requests

## Git commit

```bash
git add -A && git commit -m "feat(split-burden): add SplitCount to request DTOs and FluentValidation rules"
```
