# 405 — Freeze Period Changes Count Query Validator

## Phase

Feature — Freeze Period Discard

## Purpose

Adds FluentValidation for the `GetFreezePeriodChangesCountQuery` to ensure that `SpaceId`, `GroupId`, and `RequestingUserId` are non-empty GUIDs before the query handler executes. This enforces the security rule that all API request bodies must be validated before dispatching a command/query.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Validators/GetFreezePeriodChangesCountQueryValidator.cs` | FluentValidation validator ensuring `SpaceId`, `GroupId`, and `RequestingUserId` are non-empty |

## Key decisions

- Placed in the existing `HomeLeave/Validators` folder following the convention of other HomeLeave validators (`DeleteHomeLeaveTemplateCommandValidator`, `CancelHomeLeaveCommandValidator`, etc.)
- Used `NotEmpty()` which validates that a GUID is not `Guid.Empty` — the standard pattern across the codebase
- No custom error messages needed since the default FluentValidation messages are sufficient for GUID validation

## How it connects

- Validates the `GetFreezePeriodChangesCountQuery` record defined in `HomeLeave/Queries/GetFreezePeriodChangesCountQuery.cs`
- FluentValidation is wired via MediatR pipeline behavior, so the validator is automatically invoked before the handler runs
- Satisfies Requirement 7.4 (appropriate error response for invalid requests)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj --no-restore
```

## What comes next

- Task 1.3: Add `GetFreezePeriodChangesCount` action to `HomeLeaveConfigController`
- Task 1.4: Write unit tests for freeze-period change count query

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): add GetFreezePeriodChangesCountQuery FluentValidation validator"
```
