# 405 — Deactivate Freeze Command Validator

## Phase

Feature — Freeze Period Discard

## Purpose

Ensures that the `DeactivateFreezeWithDiscardCommand` is validated before execution, rejecting requests with empty or default GUIDs for `SpaceId`/`GroupId` and empty `RequestingUserId`. This follows the security rule that all API request bodies must be validated via FluentValidation before dispatching a command.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Validators/DeactivateFreezeWithDiscardCommandValidator.cs` | FluentValidation validator for `DeactivateFreezeWithDiscardCommand` — validates `SpaceId`, `GroupId`, and `RequestingUserId` are non-empty |

## Key decisions

- Follows the exact same pattern as `GetFreezePeriodChangesCountQueryValidator` (sibling validator in the same spec)
- Placed in the existing `HomeLeave/Validators` folder alongside other home-leave validators
- Only validates structural input correctness (non-empty IDs); business rules (freeze active, permissions) are enforced in the handler

## How it connects

- Validates the command defined in `DeactivateFreezeWithDiscardCommand.cs` (task 2.1)
- MediatR pipeline behavior automatically picks up the validator via DI assembly scanning
- Works alongside the handler (task 2.2) and controller endpoint (task 4.1)

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no new errors or warnings.

## What comes next

- Task 2.4: Unit tests for the deactivate freeze with discard command
- Task 4.1: Controller endpoint that dispatches this command

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): add DeactivateFreezeWithDiscardCommandValidator"
```
