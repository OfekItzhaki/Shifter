# 347 — Cancel Home Leave Command Validator

## Phase

Home Leave Protection — Emergency Recall Enhancement

## Purpose

Adds FluentValidation for the `CancelHomeLeaveCommand` to enforce input constraints before the handler executes. This ensures that recall operations are explicitly confirmed and that the optional reason text stays within the 500-character limit.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Validators/CancelHomeLeaveCommandValidator.cs` | New FluentValidation validator for `CancelHomeLeaveCommand` |

## Key decisions

- Follows the existing validator pattern in `HomeLeave/Validators/` (same namespace, same style).
- `Reason` validation uses `.When(x => x.Reason is not null)` to skip validation when the optional field is absent.
- `Confirmed` must equal `true` — this enforces the mandatory confirmation step at the validation layer, before the handler runs.
- Error messages are in English, consistent with other validators in the project.

## How it connects

- The MediatR pipeline's `ValidationBehavior` automatically picks up this validator via DI (FluentValidation assembly scanning is already configured).
- Works with the enhanced `CancelHomeLeaveCommand` record (task 4.1) which added `Confirmed`, `Reason`, and `ExpectedReturnAt` parameters.
- Satisfies Requirements 3.1 (confirmation required) and 8.5 (reason max 500 chars).

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no new warnings.

## What comes next

- Task 4.3: Update the command handler with confirmation and permission checks.
- Task 4.4: Property test for reason length validation (FsCheck).

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add CancelHomeLeaveCommandValidator with FluentValidation"
```
