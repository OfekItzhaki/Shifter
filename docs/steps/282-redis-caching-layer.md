# 282 — Redis Caching Layer

## Phase
Performance & Infrastructure

## Purpose
Adds a Redis-backed caching layer for the most frequently-read endpoints (schedule, live status, members) to reduce database load and improve response times. These endpoints are called on every page load and poll cycle.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Common/ICacheService.cs` | Interface defining the cache abstraction (Get, Set, Remove, RemoveByPattern) |
| `Jobuler.Infrastructure/Caching/RedisCacheService.cs` | Redis-backed implementation using StackExchange.Redis with graceful degradation |
| `Jobuler.Tests/Helpers/NoOpCacheService.cs` | No-op implementation for unit tests (always cache miss) |

### Modified files

| File | Change |
|------|--------|
| `Jobuler.Api/Program.cs` | Registered `ICacheService` → `RedisCacheService` as singleton in DI |
| `Jobuler.Application/Groups/Queries/GetGroupScheduleQuery.cs` | Added cache-aside pattern (key: `schedule:{spaceId}:{groupId}`, TTL: 30s) |
| `Jobuler.Application/Scheduling/Queries/GetGroupLiveStatusQuery.cs` | Added cache-aside pattern (key: `status:{spaceId}:{groupId}`, TTL: 30s) |
| `Jobuler.Application/Groups/Queries/GetGroupsQuery.cs` | Added cache-aside pattern to `GetGroupMembersQueryHandler` (key: `members:{spaceId}:{groupId}`, TTL: 60s) |
| `Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs` | Invalidates `schedule:*` and `status:*` caches on publish |
| `Jobuler.Application/Scheduling/Commands/DiscardVersionCommand.cs` | Invalidates `schedule:*` cache on discard |
| `Jobuler.Application/Scheduling/Commands/RollbackVersionCommand.cs` | Invalidates `schedule:*` and `status:*` caches on rollback |
| `Jobuler.Application/Groups/Commands/AddPersonToGroupByIdCommand.cs` | Invalidates `members:*` cache on member add |
| `Jobuler.Application/Groups/Commands/LeaveGroupCommand.cs` | Invalidates `members:*` cache on member remove |
| `Jobuler.Application/Groups/Commands/GroupRoleCommands.cs` | Invalidates `members:*` cache on role change |
| `Jobuler.Application/People/Commands/AddPresenceWindowCommand.cs` | Invalidates `status:*` cache on presence window change |
| `Jobuler.Application/Scheduling/IAssignmentSnapshotService.cs` | Fixed pre-existing duplicate line causing build error |
| `Jobuler.Tests/` (4 files) | Updated handler instantiations to pass `NoOpCacheService` |

## Key decisions

1. **Singleton registration** — `RedisCacheService` is registered as singleton since `IConnectionMultiplexer` is already singleton and thread-safe.
2. **Graceful degradation** — All Redis operations are wrapped in try/catch. If Redis is down, the app continues working (just without caching).
3. **Pattern-based invalidation** — On publish/rollback, we invalidate all groups in the space (`schedule:{spaceId}:*`) because the published version affects all groups.
4. **Short TTLs** — 30s for schedule/status, 60s for members. This keeps data fresh while still reducing DB load significantly on repeated page loads.
5. **System.Text.Json** — Used for serialization (consistent with the rest of the project, no new dependencies).
6. **No-op test helper** — Tests use a `NoOpCacheService` that always returns cache miss, so existing test behavior is unchanged.

## How it connects

- Uses the existing `IConnectionMultiplexer` already registered in DI (Redis is already a dependency for the solver job queue).
- The `ICacheService` interface lives in the Application layer (following the architecture rules — interfaces in Application, implementations in Infrastructure).
- Cache invalidation is co-located with the write operations that change the underlying data.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore -v q
dotnet test --no-build
```

To verify caching in action:
1. Start the API with Redis running
2. Call `GET /spaces/{id}/groups/{id}/schedule` twice — second call should be faster
3. Publish a new version — next schedule call should reflect the new data

## What comes next

- Consider adding cache metrics/logging for hit/miss ratios
- Could add caching to other read-heavy endpoints (e.g., GetGroups, GetConstraints)
- Redis health check endpoint for monitoring

## Git commit

```bash
git add -A && git commit -m "feat(infra): redis caching for schedule, live-status, and members endpoints"
```
