# 349 — Recall Audit Logging

## Phase

Home Leave Protection — Recall Audit Logging

## Purpose

Ensures every home leave recall (cancel) operation is recorded in the append-only audit log with complete accountability information. This satisfies the security and compliance requirement that all administrative actions affecting home leave are traceable.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Commands/CancelHomeLeaveCommand.cs` | Integrated `IAuditLogger` into the handler — after successful truncation/deletion, creates an audit log entry with action `cancel_home_leave`, including before-snapshot and recall metadata |

## Key decisions

- **Follows existing audit pattern**: Uses the same `IAuditLogger.LogAsync` pattern as `ApplyManualOverrideCommand`, `RollbackVersionCommand`, and other handlers — serializing before/after state as JSON.
- **Before-snapshot captures original window bounds**: The `beforeJson` includes `person_id`, `starts_at`, `ends_at`, and `operation` type (deleted/truncated) so the original state is preserved for accountability.
- **After-snapshot captures recall context**: The `afterJson` includes `reason`, `expected_return_at`, and `truncated_at` — all optional fields that are included when provided.
- **Audit runs after SaveChanges**: The audit log entry is created after the primary operation succeeds, ensuring we only log completed actions.
- **Private helper method**: Extracted `LogRecallAuditAsync` to avoid duplicating audit logic between the delete and truncate branches.

## How it connects

- Uses `IAuditLogger` interface (defined in `Application/Common/IAuditLogger.cs`, implemented in `Infrastructure/Logging/AuditLogger.cs`)
- Already registered in DI (`Program.cs`: `builder.Services.AddScoped<IAuditLogger, AuditLogger>()`)
- Satisfies Requirements 5.1, 5.2, 5.3, 5.4 from the home-leave-protection spec
- The audit entry follows the same schema as other audit entries (action, entity_type, entity_id, beforeJson, afterJson)

## How to run / verify

```bash
cd apps/api
dotnet build
dotnet test
```

The audit log entry will be created in the `audit_logs` table whenever a home leave recall succeeds. Verify by:
1. Triggering a recall via the API
2. Querying `SELECT * FROM audit_logs WHERE action = 'cancel_home_leave'`
3. Confirming `before_json` contains original window times and operation type
4. Confirming `after_json` contains reason and expected return time (if provided)

## What comes next

- Task 6.2: Property test for audit log completeness (validates all required fields are present)
- Task 9.1: Wire recall notification dispatch into the handler

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): integrate audit logging into CancelHomeLeaveCommand handler"
```
