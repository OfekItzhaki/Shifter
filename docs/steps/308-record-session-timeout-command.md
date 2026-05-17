# 308 — RecordSessionTimeoutCommand

## Phase

Admin Session Timeout feature — Backend session timeout event recording

## Purpose

When an elevated mode session (Management Mode or Super Platform Mode) is terminated due to inactivity, the system needs to record this event in the audit log for security auditing purposes. This command handles that responsibility.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Auth/Commands/RecordSessionTimeoutCommand.cs` | MediatR command record (`UserId`, `SpaceId?`, `Mode`) and handler that creates an audit log entry via `IAuditLogger` |

## Key decisions

- **No DB context needed** — the handler delegates entirely to `IAuditLogger`, which handles persistence internally. This keeps the handler minimal and focused.
- **Mode stored in afterJson** — the timeout mode ("management" or "platform") is serialized into the `afterJson` field of the audit log entry, following the pattern used by other audit entries that store contextual metadata.
- **No validator** — the command is dispatched internally (from the API controller after authorization), so input validation is handled at the controller level.

## How it connects

- Called by `AuthController` at `POST /auth/session-timeout-event` (task 4.2)
- Uses `IAuditLogger` interface (defined in `Application/Common/IAuditLogger.cs`, implemented in `Infrastructure/Logging/AuditLogger.cs`)
- Frontend sends this event when an elevated mode session times out (task 9.4)

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no new warnings.

## What comes next

- Task 3.2: Extend `UpdateGroupSettingsCommand` with `ManagementTimeoutMinutes`
- Task 3.3: Create `UpdatePlatformSettingsCommand`
- Task 4.2: Wire the `POST /auth/session-timeout-event` endpoint in `AuthController`

## Git commit

```bash
git add -A && git commit -m "feat(auth): add RecordSessionTimeoutCommand for audit logging"
```
