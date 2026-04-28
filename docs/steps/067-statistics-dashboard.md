# Step 067 — Statistics & Analytics Dashboard

## Phase
Phase 7 — Analytics & Observability

## Purpose
Expose burden and fairness statistics to admins so they can monitor scheduling equity across the space. Surfaces per-person assignment history, hated/disliked/favorable task counts, burden scores, and leaderboards — all derived from the existing `fairness_counters` and `assignments` tables.

## What was built

### Backend

**`apps/api/Jobuler.Application/Scheduling/Queries/GetBurdenStatsQuery.cs`** (modified)
- Fixed bug: `TaskSlot` has no `.TaskType` navigation property — replaced with a two-step join: fetch `TaskSlot.TaskTypeId` first, then look up `TaskType.BurdenLevel` via a dictionary.
- Added new fields to `PersonBurdenStatsDto`: `FavorableTasksAllTime`, `GroupsCount`, `LastAssignmentDate`, `AverageAssignmentsPerWeek`, `BurdenBalance`.
- Added new leaderboards to `BurdenStatsDto`: `MostFavorableTasks`, `BestBurdenBalance`, `WorstBurdenBalance`, `MostConsecutiveBurden`.
- Added space-level summary fields: `TotalPeople`, `TotalGroups`, `TotalPublishedVersions`, `AverageAssignmentsPerPerson`, `MostBurdenedPersonId`, `LeastBurdenedPersonId`.

**`apps/api/Jobuler.Api/Controllers/StatsController.cs`** (new)
- `GET /spaces/{spaceId}/stats/burden` — requires `space.view` permission, returns `BurdenStatsDto`.

### Frontend

**`apps/web/lib/api/schedule.ts`** (modified)
- Added `PersonBurdenStats`, `LeaderboardEntry`, `BurdenStats` TypeScript interfaces.
- Added `getBurdenStats(spaceId)` API function.

**`apps/web/app/admin/stats/page.tsx`** (new)
- Admin-only stats dashboard (redirects non-admins).
- 4 summary cards: Total People, Total Assignments, Avg per Person, Published Versions.
- 2×2 leaderboard grid: Most Assignments, Most Hated Tasks, Highest Burden Score, Best Burden Balance.
- Full sortable people table: Name | Total | Hated | Disliked | Favorable | Burden Score | Balance | Last Assignment.
- Color coding: hated tasks in red, favorable in green, negative balance in amber.

**`apps/web/app/admin/stats/_components/StatsLeaderboard.tsx`** (new)
- Reusable ranked leaderboard card with medal-style rank badges and avatar initials.

**`apps/web/app/admin/stats/_components/StatsPeopleTable.tsx`** (new)
- Sortable table component for per-person burden breakdown. Click any column header to sort.

**`apps/web/components/shell/AppShell.tsx`** (modified)
- Added "סטטיסטיקות" nav item pointing to `/admin/stats`, shown only when `adminGroupId !== null`.

## Key decisions
- The `TaskSlot → TaskType` join is done in two queries (fetch slot→typeId, then typeId→burdenLevel) to avoid the missing navigation property without requiring a schema change.
- `GroupTask` IDs can appear as `TaskSlotId` in assignments (solver uses group tasks directly), so burden lookup checks both tables.
- `BurdenBalance = FavorableTasksAllTime - HatedTasksAllTime` — positive means well-treated, negative means overloaded.
- `AverageAssignmentsPerWeek = TotalAssignments30d / 4.0` — approximation using the rolling 30d counter.
- Frontend page is split into 3 files to stay under 300 lines each.

## How it connects
- Reads from `fairness_counters` (rolling ledger updated by solver), `assignments` (published versions only), `task_slots`, `task_types`, `group_tasks`, `group_memberships`, `schedule_versions`.
- Uses the same `IPermissionService` pattern as all other controllers — `space.view` is sufficient (read-only stats).
- Nav item uses the existing `adminGroupId` signal from `authStore` — no new auth state needed.

## How to run / verify
1. Start the API and navigate to `GET /spaces/{id}/stats/burden` — should return JSON with `people`, leaderboards, and space totals.
2. In the web app, log in as an admin and click "סטטיסטיקות" in the sidebar — the dashboard should load.
3. Click column headers in the people table to verify sorting works.
4. Non-admin users should be redirected away from `/admin/stats`.

## What comes next
- Trend charts (assignments over time per person) using a time-series query.
- Export stats to CSV.
- Automated fairness alerts when `BurdenBalance` drops below a threshold.

## Git commit
```bash
git add -A && git commit -m "feat(stats): burden & fairness statistics dashboard - leaderboards, per-person breakdown, admin nav"
```
