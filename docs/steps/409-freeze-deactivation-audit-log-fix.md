# 409 — Freeze Deactivation Audit Log Fix

## Phase

Feature: Freeze Period Discard — Audit Logging (Task 5.1)

## Purpose

Verify and fix the audit log entries for freeze deactivation actions. The `deactivate_freeze` audit log was capturing `config.FreezeStartedAt` **after** `DeactivateEmergencyFreeze()` had already set it to `null`, resulting in a missing `freeze_started_at` in the before-snapshot.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/HomeLeave/Commands/DeactivateFreezeWithDiscardCommand.cs` | Capture `FreezeStartedAt` before calling `DeactivateEmergencyFreeze()` so the audit log before-snapshot contains the correct value |

## Key decisions

- The fix captures `config.FreezeStartedAt` into a local variable before the domain method clears it, ensuring the audit log always records the actual freeze start timestamp.
- No structural changes to the audit logging pattern — the existing `discard_freeze_changes` log was already correct since it uses a local `freezeStartedAt` variable captured earlier in the discard branch.

## How it connects

- Satisfies Requirements 3.4, 4.3, 4.4 from the freeze-period-discard spec
- Ensures the audit trail is complete and accurate for compliance and debugging

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build should succeed with no new warnings.

## What comes next

- Task 5.2: Write unit tests for audit log entries
- Task 7: Frontend deactivation dialog

## Git commit

```bash
git add -A && git commit -m "fix(audit): capture freeze_started_at before deactivation clears it"
```
