# Step 285 — Cross-Group Conflict Detection: Integration Hooks & DI Registration

## Phase

Phase 7 — Cross-Group Conflict Detection (Integration)

## Purpose

Wire the conflict detection service into the existing publish and login flows via fire-and-forget `Task.Run`, and register all conflict detection services in the DI container. This makes the feature active without affecting response times or success/failure of the main operations.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs` | Added fire-and-forget `Task.Run` block after audit log and external notifications to call `IConflictDetectionService.DetectOnPublishAsync` |
| `apps/api/Jobuler.Application/Auth/Commands/LoginCommandHandler.cs` | Added `IServiceScopeFactory` and `ILogger` dependencies; added fire-and-forget `Task.Run` block after `SaveChangesAsync` to call `IConflictDetectionService.DetectOnLoginAsync` |
| `apps/api/Jobuler.Api/Program.cs` | Registered `ConflictDetectionDbContext` (same connection string, no RLS interceptor) and `IConflictDetectionService` → `ConflictDetectionService` as scoped |

## Key decisions

- **Reused existing fire-and-forget pattern**: The `PublishVersionCommandHandler` already uses `Task.Run` with a new DI scope for external notifications. The conflict detection hook follows the exact same pattern for consistency.
- **Separate scope per catch block**: The error logging creates a new scope to resolve `ILogger` because the original scope may be disposed if the error occurs during scope disposal. This avoids potential `ObjectDisposedException`.
- **No RLS on ConflictDetectionDbContext**: Registered with the same `DefaultConnection` string but without any RLS interceptor, allowing cross-space queries for `LinkedUserId` resolution.
- **Login hook captures userId before Task.Run**: Avoids closure over the `user` entity which belongs to the request-scoped DbContext.

## How it connects

- Depends on: `IConflictDetectionService` (Task 4.1), `ConflictDetectionService` (Task 5.2/5.3), `ConflictDetectionDbContext` (Task 5.1)
- Consumed by: End users receive conflict notifications after schedule publish or login
- The fire-and-forget pattern ensures zero impact on publish/login response times

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with 0 errors. Integration tests (Task 7.4) will verify the hooks work end-to-end.

## What comes next

- Task 7.4: Unit tests for integration points (verify error isolation)
- Task 8.1: Full step documentation for the entire feature

## Git commit

```bash
git add -A && git commit -m "feat(conflicts): hook conflict detection into publish/login handlers and register DI services"
```
