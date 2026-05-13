# Step 230 — Production Bugfixes: i18n, Join Flow, Changelog, Stats Redirect

## Phase

Phase 5 — Polish & Production Readiness

## Purpose

Fix four issues found during production testing:
1. Error messages displayed in English instead of Hebrew
2. Invitation link flow not handling already-logged-in users properly
3. Changelog missing recent features
4. Global stats page redundant with group-level stats tab

## What was built

### Issue 1: i18n for StatsTab and related components

- **`apps/web/app/groups/[groupId]/tabs/StatsTab.tsx`** — Replaced all hardcoded English strings ("Error loading statistics", "Connection timeout — try again", "No statistics for this group", "Publish a schedule to see data", "Loading statistics...", summary card labels, leaderboard titles, "Detail by Person") with `useTranslations("groups.stats_tab")` calls.
- **`apps/web/app/admin/stats/_components/StatsLeaderboard.tsx`** — Replaced "No data" with `t("noData")` from `useTranslations("admin")`.
- **`apps/web/app/admin/stats/_components/StatsPeopleTable.tsx`** — Replaced English column headers (Name, Total, Hated, Disliked, Favorable, Burden Score, Balance, Last Assignment) with translated keys from `useTranslations("groups.stats_tab.table")`.
- **`apps/web/messages/he.json`** — Added `loading`, `connectionTimeout`, `errorLoading`, and `table.*` keys to `groups.stats_tab`.
- **`apps/web/messages/en.json`** — Added matching English keys.

### Issue 2: Invitation link flow for already-logged-in users

- **`apps/api/Jobuler.Application/Groups/Commands/JoinCodeCommands.cs`** — Added `AlreadyMember` boolean to `JoinGroupResult` record. The handler now passes the `alreadyMember` flag it already computes.
- **`apps/web/lib/api/groups.ts`** — Updated `joinGroupByCode` return type to include `alreadyMember`.
- **`apps/web/app/groups/join/page.tsx`** — Added `useEffect` to auto-join when user is authenticated and code is in URL. Shows "כבר חבר בקבוצה" when `alreadyMember` is true.
- **`apps/web/messages/he.json`** — Added `alreadyMember` key to `groups.join`.
- **`apps/web/messages/en.json`** — Added `alreadyMember` key to `groups.join`.

### Issue 3: Changelog update

- **`apps/web/app/changelog/page.tsx`** — Added version 1.6.0 entry with all new features: home-leave scheduling, biometric login, statistics graphs, color-coded roles, qualification templates, unavailability reasons, burden level simplification.

### Issue 4: Global stats page redirect

- **`apps/web/app/stats/page.tsx`** — Replaced the full stats dashboard with a redirect to `/groups`. Shows a brief Hebrew message explaining stats now live inside each group's tab.

## Key decisions

- Used error codes (`"timeout"`, `"loadError"`) internally in StatsTab state, then resolved to translated strings at render time — keeps the logic clean.
- Backend returns `alreadyMember` flag rather than a different HTTP status, maintaining backward compatibility.
- Auto-join triggers via `useEffect` when code is present in URL and user is authenticated — no manual button click needed.
- Global `/stats` page redirects to `/groups` rather than being deleted, to avoid breaking bookmarks.
- Changelog entries written in Hebrew since the app's primary audience is Hebrew-speaking.

## How it connects

- Translation keys follow the existing `groups.stats_tab.*` namespace convention.
- `JoinGroupResult` record change is backward-compatible (new field with default).
- The group-level StatsTab is the canonical stats view; the global page now redirects there.

## How to run / verify

1. Navigate to a group → Stats tab. Verify all text is in Hebrew.
2. Open `/groups/join?code=ANYCODE` while logged in. Verify auto-join triggers.
3. Join a group you're already in. Verify "כבר חבר בקבוצה" message appears.
4. Open `/changelog`. Verify version 1.6.0 appears at the top.
5. Navigate to `/stats`. Verify redirect to `/groups`.

## What comes next

- Remove the `/stats` nav item from the sidebar (or keep it as a shortcut to the first group's stats).
- Add Russian translations for the new keys.

## Git commit

```bash
git add -A && git commit -m "fix(phase5): i18n stats errors, join flow already-member, changelog v1.6, stats redirect"
```
