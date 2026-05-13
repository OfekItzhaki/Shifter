# Step 232 — Timezone Fix, Stats Crash, Platform Hydration

## Phase

Phase 5 — Polish & Production Readiness

## Purpose

Fix four production bugs found during testing:
1. "My Missions" page shows "no missions this week" despite having active assignments
2. Live Status shows everyone as "פנוי בבסיס" despite active shifts
3. Stats tab crashes with `r.map is not a function`
4. Platform page sometimes redirects to login when already authenticated

## What was built

### Bug 1 & 2: Timezone mismatch (same root cause)

**Root cause:** Task times are stored in Israel local time (UTC+3) but the API compared them against `DateTime.UtcNow`. At 23:54 Israel time, `UtcNow` is 20:54 — a shift starting at 21:00 local appears to not have started yet.

- **`apps/api/Jobuler.Application/Scheduling/Queries/GetGroupLiveStatusQuery.cs`** — Changed `var now = DateTime.UtcNow` to use Israel timezone (`Asia/Jerusalem` on Linux, `Israel Standard Time` on Windows). Now correctly identifies active shifts.
- **`apps/api/Jobuler.Api/Controllers/ScheduleVersionsController.cs`** — Changed `MyAssignmentsController.Get()` to use Israel timezone for the date range calculation. "Today" now means today in Israel, not today in UTC.

### Bug 3: Stats tab crash

**Root cause:** `StatsLeaderboard` component called `.map()` on `entries` which could be `undefined` if the API response had unexpected shape. Also `RotationProgressCard` expected `data.entries` but API returns `{ people: [...] }`.

- **`apps/web/app/groups/[groupId]/tabs/StatsTab.tsx`** — Added `?? []` fallback to all leaderboard array props and the people table prop.
- **`apps/web/components/stats/RotationProgressCard.tsx`** — Fixed data extraction to handle `{ people: [...] }` response shape correctly with proper array validation.

### Bug 4: Platform page redirect

**Root cause:** Zustand persist middleware hadn't finished rehydrating from localStorage when the component checked `isAuthenticated`. The simple `setHydrated(true)` in useEffect ran before the store was ready.

- **`apps/web/app/platform/page.tsx`** — Replaced naive hydration check with `useAuthStore.persist.onFinishHydration()` callback. Now waits for the actual store rehydration before checking auth state.

### Bug 5 (confirmed already working): "At home" status

The LiveStatusPanel already supports `at_home` status with amber badge styling. It shows when a person has an active PresenceWindow with state `at_home`. No changes needed.

## Key decisions

- Used `TimeZoneInfo.FindSystemTimeZoneById` with OS detection for cross-platform compatibility (Windows uses "Israel Standard Time", Linux uses "Asia/Jerusalem").
- Added defensive `?? []` fallbacks in the frontend rather than fixing the API response — this makes the UI resilient to any future API changes.
- Used Zustand's built-in `persist.onFinishHydration()` API rather than a custom timeout hack.

## How it connects

- The timezone fix affects all time-based comparisons in the live status and my-assignments features.
- The stats crash fix makes the frontend resilient to partial/unexpected API responses.
- The platform hydration fix prevents false redirects on slow connections.

## How to run / verify

1. Open "My Missions" page — should show today's assignments.
2. Open Live Status tab — people with active shifts should show "במשימה" (on mission).
3. Open Stats tab — should load without crashing.
4. Navigate to Platform page while logged in — should not redirect to login.
5. Check that "at home" status shows amber badge when a person has an active at_home presence window.

## What comes next

- Consider storing all times in UTC and converting on display (proper long-term fix)
- Add timezone configuration per space (currently hardcoded to Israel)

## Git commit

```bash
git add -A && git commit -m "fix(phase5): timezone fix for live-status/missions, stats crash guard, platform hydration"
```
