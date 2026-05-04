# Step 105 — Publish Fix (Audit Logger Race), Notifications Bell Fix, Solver Start Time Fix

## Phase
Phase 9 — Polish & Hardening

## Purpose
Three bugs persisted after step 103:

1. **Publish still failing** — The `AuditLogger` uses the same `AppDbContext` as `PublishVersionCommand`. The audit log call was placed AFTER the fire-and-forget `Task.Run`, meaning the background task could start a DB operation on the same context concurrently with the audit log's `SaveChangesAsync`. Fixed by moving the audit log call BEFORE the fire-and-forget. Also added a double-submit guard on the frontend.

2. **Notifications bell still not opening** — The dropdown used `position: "fixed"` with hardcoded `left: 268` and `z-[100]` (Tailwind class). The sidebar has `zIndex: 30` as an inline style, and the Tailwind class wasn't reliably overriding it. Fixed by using `zIndex: 9999` as an inline style and positioning the dropdown at `top: 60, left: 16` (below the sidebar header, inside the viewport).

3. **Solver start time not used** — `horizonStart` (the date sent to the solver as `horizon_start`) was always hardcoded to `today`, ignoring the `startTime` parameter. Also `horizonEnd` was computed from `today` instead of `horizonStart`. Fixed both.

## What was built

### `apps/api/Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs`
- Moved `await _audit.LogAsync(...)` to BEFORE the `Task.Run(...)` fire-and-forget block. The audit logger uses the same `_db` instance — keeping it sequential (before the background task starts) eliminates the concurrency window.
- Added a guard: if `version.Status != Draft`, throw `InvalidOperationException` immediately (prevents confusing errors if the version was already published by a concurrent request).

### `apps/web/app/groups/[groupId]/page.tsx`
- Added `if (publishSaving) return;` at the top of `handlePublish` to prevent double-submit. The `DraftScheduleModal` and the schedule tab both have publish buttons — clicking both quickly caused two concurrent API calls.

### `apps/web/components/shell/NotificationBell.tsx`
- Changed dropdown from `z-[100]` (Tailwind) to `zIndex: 9999` (inline style) — reliably overrides the sidebar's `zIndex: 30`.
- Changed position from `top: 12, left: 268` (hardcoded, breaks in RTL) to `top: 60, left: 16` (below the sidebar header, always visible).
- Changed `<div ref={ref} className="relative">` to `style={{ position: "relative" }}` — avoids Tailwind class conflicts.
- Added `type="button"` to all buttons inside the bell to prevent accidental form submission.

### `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs`
- `horizonStart` now derives from `startTime` when provided: `DateOnly.FromDateTime(nowUtc)` instead of always `today`.
- `horizonEnd` now computes from `horizonStart` instead of `today`.
- This means when an admin sets a custom start time (e.g. tomorrow 08:00), the solver horizon starts from that date and extends `maxHorizon` days forward.

## Key decisions

- **Audit log before fire-and-forget**: The audit log is a synchronous sequential operation on `_db`. Moving it before `Task.Run` ensures it completes before the background task can touch `_db` in any way (even though the background task now uses its own scope, the ordering is cleaner and eliminates any theoretical race).
- **Inline zIndex for bell**: Tailwind's `z-[100]` class requires the Tailwind JIT to generate it. Using `zIndex: 9999` as an inline style is guaranteed to work regardless of Tailwind config.
- **horizonStart from startTime**: The `horizon_start` field in the solver payload controls which day the stability weights start from. If the admin sets a future start time, the solver should treat that day as "day 0" for stability calculations.

## How to verify

1. Publish a draft — should succeed without the DbContext error.
2. Click the notification bell — dropdown should open at the top-left of the sidebar, below the logo.
3. Set a future start time in the admin schedule page and run the solver — check the API logs for `"Solver payload built: ... horizon=<date>→<date>"` to confirm the horizon starts from the selected date.

## Git commit

```bash
git add -A && git commit -m "fix(publish): audit logger race condition; fix notifications bell z-index; fix solver horizonStart from startTime"
```
