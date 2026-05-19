# Step 415 — Freeze Deactivation Audit Log Tests

## Phase

Feature: freeze-period-discard (Task 5.2)

## Purpose

Verifies that the `DeactivateFreezeWithDiscardCommandHandler` produces correct audit log entries for all three scenarios: discard actions, non-discard deactivations, and denied permission attempts. These tests ensure compliance with requirements 3.4, 4.3, 4.4, and 5.4.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/HomeLeave/DeactivateFreezeAuditLogTests.cs` | Unit tests verifying audit log entries for freeze deactivation scenarios |

## Key decisions

- Used NSubstitute to mock `IAuditLogger` and verify calls with argument matchers for JSON content
- Used `Arg.Is<string?>` matchers to verify JSON payloads contain required fields without being brittle to exact serialization format
- Tested three distinct scenarios: discard with full fields, non-discard with `discard_performed: false` flag, and permission denied with action attempted details
- Used reflection to set `PublishedAt` and `CreatedAt` on entities for precise time-based test scenarios

## How it connects

- Tests the audit logging behavior implemented in step 407 (DeactivateFreezeWithDiscardHandler)
- Validates the audit log entries defined in step 408 (permission denied audit logging)
- Complements the permission enforcement tests in step 413

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~DeactivateFreezeAuditLogTests"
```

## What comes next

- Task 6: Final checkpoint — full integration verification
- Task 7: Frontend deactivation dialog implementation

## Git commit

```bash
git add -A && git commit -m "feat(freeze-period-discard): audit log unit tests for deactivation handler"
```
