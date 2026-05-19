# 403 — DeactivateFreezeWithDiscardCommand and Result Records

## Phase

Feature — Freeze Period Discard

## Purpose

Defines the MediatR command and result records for deactivating an emergency freeze with an optional discard of freeze-period schedule changes. This is the contract that the handler (task 2.2) and API endpoint (task 4.1) will use.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Commands/DeactivateFreezeWithDiscardCommand.cs` | Command record (`DeactivateFreezeWithDiscardCommand`) and result record (`DeactivateFreezeResult`) |

## Key decisions

- **Reuses `HomeLeaveConfigResult`** — The result record references the existing `HomeLeaveConfigResult` to return the updated config state after deactivation, avoiding duplication.
- **Nullable `DiscardVersionId`** — When discard is not performed (or zero changes exist), the version ID is null.
- **Follows existing pattern** — Command and result records live in the same file under `Jobuler.Application.HomeLeave.Commands`, matching `CancelHomeLeaveCommand` and `UpsertHomeLeaveConfigCommand`.

## How it connects

- **Task 2.2** will add the `IRequestHandler<DeactivateFreezeWithDiscardCommand, DeactivateFreezeResult>` implementation in this same file.
- **Task 2.3** will add a FluentValidation validator for this command.
- **Task 4.1** will dispatch this command from `HomeLeaveConfigController`.
- The `HomeLeaveConfigResult` type is already defined in `UpsertHomeLeaveConfigCommand.cs`.

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no new errors.

## What comes next

- Task 2.2: Implement the command handler with permission checks, discard logic, and audit logging.
- Task 2.3: Add FluentValidation validator for the command.

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): add DeactivateFreezeWithDiscardCommand and result records"
```
