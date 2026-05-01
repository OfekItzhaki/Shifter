# Step 083 — Live Status Panel & Spec Completion

## Phase
Phase 5 — Frontend Features (schedule-table-autoschedule-role-constraints spec)

## Purpose
Complete the final two tasks of the `schedule-table-autoschedule-role-constraints` spec:
- Task 33: Live status panel UI component
- Task 34: Final checkpoint — all builds and tests verified

This closes out the full spec, which covered 2D schedule table, auto-scheduler gap detection, role constraints, manual overrides, and live member status.

## What was built

### `apps/web/components/schedule/LiveStatusPanel.tsx` (new)
- Polls `GET /spaces/{spaceId}/groups/{groupId}/live-status` every 30 seconds
- Groups members by status: `on_mission` (blue), `free_in_base` (green), `at_home` (amber), `blocked` (red)
- Shows task name and slot end time for `on_mission` members
- Displays last-updated timestamp and a manual refresh button
- Hebrew labels throughout; loading spinner and error state handled
- Empty state when group has no members

### `apps/web/app/groups/[groupId]/page.tsx` (modified)
- Added `"live-status"` to `ActiveTab` union and `ALL_TABS` array
- Added `"סטטוס נוכחי"` label to `TAB_LABELS`
- Renders `<LiveStatusPanel spaceId={currentSpaceId} groupId={groupId} />` when `activeTab === "live-status"`
- Tab is visible to all members (not admin-only)

### `apps/web/lib/api/groups.ts` (modified)
- Added `MemberLiveStatusDto` interface
- Added `getGroupLiveStatus(spaceId, groupId)` API function

### `apps/web/app/groups/[groupId]/types.ts` (modified)
- Added `"live-status"` to `ActiveTab` type

## Key decisions
- Live status tab is not in `ADMIN_ONLY_TABS` — all group members can see it (per requirement 15.8)
- Polling interval is 30 seconds as specified in requirement 15.9
- Members are grouped by status category for readability rather than listed alphabetically

## How it connects
- Consumes `GET /spaces/{spaceId}/groups/{groupId}/live-status` (implemented in task 31/32: `GetGroupLiveStatusQuery` + `LiveStatusController`)
- Sits alongside the existing `ScheduleTab` in `GroupDetailPage`
- Uses the same `MemberLiveStatusDto` shape returned by the backend

## How to run / verify
1. Start the API: `dotnet run --project apps/api/Jobuler.Api`
2. Start the frontend: `npm run dev` (from `apps/web`)
3. Navigate to a group → click "סטטוס נוכחי" tab
4. Panel should show member statuses, refresh every 30s, and support manual refresh

### Build verification
- Frontend TypeScript: `npx tsc --noEmit` → **0 errors**
- .NET API: `dotnet build` → **Build succeeded, 0 warnings, 0 errors**
- Solver unit tests: `python -m pytest tests/ -q` → **41 passed**
- .NET unit tests: `dotnet test` → **287 passed** (12 solver integration tests require live solver on `localhost:8000` — expected to skip in CI without solver running)

## What comes next
- Tasks 19–25 (optional `*` property-based and unit tests) remain unimplemented — they are all marked optional in the spec
- The spec is functionally complete for MVP
- Next feature work can begin from a clean branch

## Git commit
```bash
git add -A && git commit -m "feat(live-status): live status panel UI, spec tasks 33-34 complete"
```
