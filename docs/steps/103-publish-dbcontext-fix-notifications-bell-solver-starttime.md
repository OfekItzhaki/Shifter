# Step 103 — Publish DbContext Fix, Notifications Bell, Solver Start Time

## Phase
Phase 9 — Polish & Hardening

## Purpose
Three bugs reported after step 102:

1. **Publish fails with DbContext concurrency error** — "A second operation was started on this context instance before a previous operation completed." The fire-and-forget `Task.Run` in `PublishVersionCommand` was sharing the request's `DbContext` with the background thread.
2. **Notifications bell can't be opened** — The bell was inside a `<Link href="/spaces">` wrapper. Clicking the bell triggered navigation instead of opening the dropdown.
3. **Solver start time** — Admins need to be able to override the calculation start time (default: now). Added a `datetime-local` picker to both the admin schedule page and the group settings tab.

## What was built

### `apps/api/Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs`
**Root cause**: `SendExternalNotificationsAsync` was called via `Task.Run(...)` but used `_db` (the request-scoped `DbContext`). EF Core `DbContext` is not thread-safe — the main handler was still using `_db` for the audit log while the background thread was querying it.

**Fix**: Inject `IServiceScopeFactory`. In the fire-and-forget block, create a new `IServiceScope` and resolve a fresh `AppDbContext` and `IScheduleNotificationSender` from it. The background task now has its own isolated DB connection.

Key change:
```csharp
_ = Task.Run(async () =>
{
    using var scope = _scopeFactory.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var notificationSender = scope.ServiceProvider.GetRequiredService<IScheduleNotificationSender>();
    await SendExternalNotificationsAsync(req, versionNumber, db, notificationSender, CancellationToken.None);
});
```

### `apps/web/components/shell/AppShell.tsx`
Moved `<NotificationBell />` outside the `<Link href="/spaces">` wrapper. It was a sibling of the logo link, not a child. Now clicking the bell opens the dropdown without navigating.

### `apps/api/Jobuler.Application/Scheduling/ISolverJobQueue.cs`
Added `DateTime? StartTime = null` to `SolverJobMessage`.

### `apps/api/Jobuler.Application/Scheduling/Commands/TriggerSolverCommand.cs`
Added `DateTime? StartTime = null` to `TriggerSolverCommand`. Passes it through to `SolverJobMessage`.

### `apps/api/Jobuler.Api/Controllers/ScheduleRunsController.cs`
Added `DateTime? StartTime = null` to `TriggerSolverRequest`. Passes it to `TriggerSolverCommand`.

### `apps/api/Jobuler.Application/Scheduling/ISolverPayloadNormalizer.cs`
Added `DateTime? startTime = null` parameter to `BuildAsync`.

### `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs`
When `startTime` is provided, uses it as `horizonStartDt` instead of `DateTime.UtcNow`. This lets admins schedule from a future or past point in time.

### `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs`
Passes `job.StartTime` to `normalizer.BuildAsync(...)`.

### `apps/web/lib/api/schedule.ts`
Added `startTime?: string` parameter to `triggerSolve()`.

### `apps/web/app/admin/schedule/page.tsx`
Added `solverStartTime` state (defaults to current local datetime). Added a `datetime-local` input next to the solver trigger buttons. Both "Run Solver" and "Emergency Schedule" pass the selected start time.

### `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx`
Added `solverStartTime` local state (defaults to now). Added a `datetime-local` picker above the "Run Schedule" button. `onTriggerSolver` now accepts an optional `startTime?: string` parameter.

### `apps/web/app/groups/[groupId]/page.tsx`
Updated `handleTriggerSolver(startTime?: string)` to pass `startTime` in the trigger request body.

### i18n (`en.json`, `he.json`, `ru.json`)
Added `admin.solverStartTime` and `groups.settings_tab.startFrom` translation keys.

## Key decisions

- **New scope for background task**: The correct EF Core pattern for fire-and-forget is always to create a new scope. Using `CancellationToken.None` in the background task is intentional — the HTTP request's cancellation token would cancel the background work when the response is sent.
- **Start time as optional override**: `null` means "use now" — existing behavior is preserved. The admin can set any future or past time.
- **datetime-local input**: Uses the browser's native datetime picker. Value is converted to ISO UTC before sending to the API.

## How to verify

1. Publish a draft schedule — should succeed without the DbContext error.
2. Click the notification bell — dropdown should open without navigating to `/spaces`.
3. In admin schedule page, change the "Start from" time and run the solver — the solver payload should use the specified start time.
4. In group settings tab, change the "Start from" time and run the schedule — same behavior.

## What comes next

- CSP headers with nonce-based approach
- Full React Query migration

## Git commit

```bash
git add -A && git commit -m "fix(publish): DbContext concurrency bug; fix notifications bell; add solver start time override"
```
