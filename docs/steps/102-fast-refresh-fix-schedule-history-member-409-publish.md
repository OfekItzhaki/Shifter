# Step 102 — Fast Refresh Fix, Schedule History, Member 409, Publish Improvements

## Phase
Phase 9 — Polish & Hardening

## Purpose
Addresses a batch of UX bugs and deferred improvements reported after LTS v1.4:

1. **Fast Refresh spam** — `[Fast Refresh] rebuilding` was printing repeatedly due to an unstable function reference in a `useEffect` dependency array.
2. **Solver failure redirect** — When the solver failed, the admin was redirected to the schedule tab, hiding the error message.
3. **Add member 409 conflict** — Adding a member who already existed in the space triggered a visible 409 HTTP error in the browser console, even though the operation succeeded.
4. **Schedule history** — The admin schedule page only showed active versions; archived/rolled-back history was hidden.
5. **Publish improvements** — After publishing, the UI didn't auto-select the newly published version. Added a Discard button to the admin schedule page.
6. **groups/[groupId]/page.tsx split** — Extracted ~200 lines of state declarations into a `useGroupPageState` hook, reducing the page component size significantly.

## What was built

### `apps/web/app/groups/[groupId]/page.tsx`
- **Fast Refresh fix**: Removed `isAdminForGroup` (an unstable function reference) from the `useEffect` dependency array for loading the group. Replaced with `adminGroupId` (a stable primitive). This was the root cause of the repeated rebuild messages.
- **Solver redirect fix**: Changed `setActiveTab("schedule")` to only fire on `status === "Completed"`, not on `Failed` or `TimedOut`. Admin now stays on the current tab when the solver fails so they can see the error.
- **Add member 409 fix**: Replaced the try/catch-on-409 pattern with a proactive search-first approach. Before creating a new person, the code now searches for an existing person with the same name. If found, it uses their ID directly, avoiding the 409 entirely.
- **State extraction**: Replaced ~200 lines of inline `useState` declarations with a single `useGroupPageState()` hook call.

### `apps/web/app/groups/[groupId]/useGroupPageState.ts` (new)
Custom hook that owns all state for `GroupDetailPage`. Groups state by tab (schedule, members, alerts, messages, tasks, constraints, settings, qualifications, roles). Keeps the page component focused on effects and handlers.

### `apps/web/app/admin/schedule/page.tsx`
- **Schedule history**: `VersionListSidebar` now separates active versions (Draft/Published) from history (Archived/RolledBack/Discarded). History is collapsed by default behind a toggle. History versions are grouped by publish day with timestamps.
- **Auto-select after publish**: `loadVersions()` now falls back to selecting the most recent Published version when no Draft exists, so the UI updates correctly after publishing.
- **Discard button**: Added a Discard button for Draft versions in `VersionDetailPanel`.
- **`handleDiscard`**: New handler that calls `discardVersion()` and reloads the version list.

### `apps/web/lib/api/schedule.ts`
- Added `discardVersion(spaceId, versionId)` function (calls `DELETE /schedule-versions/{id}`).

### `apps/web/messages/en.json`, `he.json`, `ru.json`
- Added `admin.discard` button label.
- Added `admin.schedule.discardSuccess` and `admin.schedule.discardError` toast messages.

## Key decisions

- **Search-first for add member**: Avoids the 409 entirely rather than catching it. The search query is cheap (2-char minimum, indexed). This is cleaner than swallowing HTTP errors.
- **History collapsed by default**: Keeps the sidebar clean for the common case (working with the current draft/published version). History is one click away.
- **State hook, not context**: `useGroupPageState` is a plain hook, not a React context. This avoids re-render propagation issues and keeps the refactor minimal — the page still owns all the handlers.

## How to connect

- The `useGroupPageState` hook is only used by `GroupDetailPage`. It's not shared.
- The `discardVersion` API function mirrors the existing `publishVersion` / `rollbackVersion` pattern.

## How to verify

1. Open a group page — the browser console should no longer spam `[Fast Refresh] rebuilding`.
2. Trigger the solver from the settings tab. When it fails, you should stay on the settings tab and see the error.
3. Add a member who already exists in the space — no 409 in the console, member is added.
4. In the admin schedule page, publish a draft — the sidebar should auto-select the published version.
5. In the admin schedule page, click "History" in the sidebar — archived versions appear grouped by day.
6. Click "Discard" on a draft version — it disappears and the sidebar updates.

## What comes next

- CSP headers with nonce-based approach (deferred)
- Full React Query migration for remaining pages (deferred)
- Schedule calculation from current time — already implemented in `SolverPayloadNormalizer` (`horizonStartDt = nowUtc`)

## Git commit

```bash
git add -A && git commit -m "fix(ux): fast-refresh spam, solver redirect, member 409, schedule history, publish improvements"
```
