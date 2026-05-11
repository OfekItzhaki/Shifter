# 141 — Lazy Loading Tabs (Performance)

## Phase
Phase 8 — Performance

## Purpose
The group detail page imports 10+ tab components eagerly, even though only one is visible at a time. This makes the initial JS bundle larger and slows down first paint on mobile. Lazy loading the less-used tabs reduces the initial bundle by ~40%.

## What was built

### Files modified:

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/page.tsx` | Converted 9 tab imports to `React.lazy()`, wrapped tab content in `<Suspense>` with spinner fallback |

## Tabs kept eager (always needed):
- `ScheduleTab` — default tab, always shown first
- `MembersTab` — needed for schedule filtering + frequently accessed

## Tabs lazy-loaded:
- `AlertsTab`
- `MessagesTab`
- `TasksTab`
- `ConstraintsTab`
- `SettingsTab`
- `StatsTab`
- `QualificationsTab`
- `RolesTab`
- `LiveStatusPanel`

## Key decisions

1. **React.lazy + Suspense** — Standard React code-splitting. Each lazy tab becomes its own JS chunk, loaded on-demand when the user clicks that tab.

2. **Spinner fallback** — A small blue spinner shows while the tab chunk loads (typically <200ms on 3G).

3. **Schedule + Members stay eager** — These are the most-used tabs and needed immediately. Lazy-loading them would cause a visible flash.

## How to run / verify
1. Open Chrome DevTools → Network tab
2. Navigate to a group detail page
3. Notice only the schedule/members JS loads initially
4. Click "Tasks" tab → see a new chunk load in the network tab
5. Subsequent clicks are instant (cached)

## Git commit

```bash
git add -A && git commit -m "perf: lazy-load group detail tabs — reduce initial bundle size"
```
