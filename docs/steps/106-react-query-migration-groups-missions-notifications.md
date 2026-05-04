# Step 106 — React Query Migration: Groups, My Missions, Notifications

## Phase
Phase 9 — Polish & Hardening

## Purpose
Continued the gradual React Query migration. Three high-traffic pages were still using direct `apiClient` calls in `useEffect` hooks. Migrated them to React Query for automatic caching, deduplication, and background refetching.

Also confirmed all spec tests (tasks 19–25) are passing and marked them complete.

## What was built

### `apps/web/lib/query/hooks/useGroups.ts` (new)
React Query hooks for the groups list:
- `useGroups(spaceId)` — fetches active groups, 30s stale time
- `useDeletedGroups(spaceId)` — fetches deleted groups, 60s stale time
- `useCreateGroup(spaceId)` — mutation that invalidates the groups query on success
- `useRestoreGroup(spaceId)` — mutation that invalidates both groups and deleted-groups queries

### `apps/web/lib/query/hooks/useMyAssignments.ts` (new)
React Query hook for the user's own assignments:
- `useMyAssignments(spaceId, range)` — fetches assignments for the given range (`today`/`week`/`month`/`year`), 60s stale time
- Exports `AssignmentRange` type and `MyAssignmentDto` interface

### `apps/web/app/groups/page.tsx`
Migrated from `useEffect + apiClient.get` to `useGroups` + `useDeletedGroups` + `useCreateGroup` + `useRestoreGroup`. Removed all manual state management for loading/data — React Query handles it. The page is now ~40 lines shorter.

### `apps/web/app/schedule/my-missions/page.tsx`
Migrated from `useEffect + apiClient.get` to `useMyAssignments`. Removed the `loading` state and the `useEffect` entirely. The range selector now just changes the query key, and React Query handles the refetch automatically.

### `apps/web/app/notifications/page.tsx`
Migrated from `useEffect + apiClient.get` to `useNotifications` + `useDismissNotification` + `useDismissAllNotifications` (hooks already existed). Added a "Mark all read" button to the full page view (was only in the bell dropdown before). Removed all manual state management.

## Test status
All 357 backend tests pass. All 51 solver tests pass. All frontend logic tests pass.

## Remaining pages still using direct apiClient
- `groups/[groupId]/page.tsx` — complex page with many tabs; migration deferred (state hook already extracted)
- `admin/groups/page.tsx` — admin-only page
- `admin/people/[personId]/page.tsx` — admin-only page
- `schedule/today/page.tsx`, `schedule/tomorrow/page.tsx` — simple pages, low priority
- `group-opt-out/[token]/page.tsx` — one-time action page, no caching needed

## Git commit

```bash
git add -A && git commit -m "feat(react-query): migrate groups, my-missions, notifications pages; add useGroups and useMyAssignments hooks"
```
