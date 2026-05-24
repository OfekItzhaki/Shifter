# 534 — UpdateSpaceCommand Name Validation

## Phase

Space Management — Application Layer Settings Commands

## Purpose

Adds FluentValidation to the `UpdateSpaceCommand` to enforce that the space name is between 1 and 100 characters after trimming whitespace. This aligns with Requirement 7.2/7.3 and ensures validation runs in the MediatR pipeline before the handler executes.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Spaces/Validators/UpdateSpaceCommandValidator.cs` | New FluentValidation validator that checks Name is 1–100 chars after trim, SpaceId and RequestingUserId are non-empty |
| `Jobuler.Application/Spaces/Commands/UpdateSpaceCommand.cs` | Updated inline validation from 2–100 to 1–100 chars to match the spec requirement |

## Key decisions

- **Dual validation**: The FluentValidation validator runs in the MediatR pipeline (via `ValidationBehavior`) and catches invalid names before the handler. The handler retains inline validation as a defense-in-depth measure.
- **Trim-aware validation**: The validator uses `.Must()` with explicit `Trim()` logic to validate the trimmed length, matching the spec requirement that validation applies "after trimming whitespace."
- **Error message**: Uses the exact message from the design doc error handling table: "Space name must be between 1 and 100 characters."
- **Minimum changed from 2 to 1**: The original handler used a minimum of 2 characters. The spec explicitly requires 1 character minimum, so both the validator and handler now enforce 1–100.

## How it connects

- The `ValidationBehavior<TRequest, TResponse>` pipeline behavior auto-discovers this validator via DI and runs it before `UpdateSpaceCommandHandler`.
- `ExceptionHandlingMiddleware` maps `FluentValidation.ValidationException` to HTTP 400 with field-keyed errors.
- The handler's inline `InvalidOperationException` also maps to HTTP 400 via the middleware.

## How to run / verify

```bash
cd apps/api
dotnet build
dotnet test --filter "UpdateSpace"
```

## What comes next

- Task 7.5: `RegenerateSpaceInviteCodeCommand` and handler
- Task 7.6: Property tests for settings commands (Properties 8, 9, 10, 11)

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add FluentValidation for UpdateSpaceCommand name (1-100 chars)"
```
