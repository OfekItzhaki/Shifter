# 138 — Schedule Diff View

## Phase
Phase 8 — UX & Mobile

## Purpose
When a new schedule is published, admins and soldiers want to quickly see what changed compared to the previous version. Instead of comparing two full schedules manually, the diff view highlights additions, removals, and person swaps in a clear, color-coded list.

## What was built

### Files created:

| File | Description |
|------|-------------|
| `apps/web/components/schedule/ScheduleDiffView.tsx` | Full diff view component — fetches both versions, computes client-side diff, shows color-coded entries with filtering |

### Files modified:

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx` | Added "Changes" button (admin-only) and renders ScheduleDiffView panel |
| `apps/web/messages/he.json` | Added `schedule.diff.*` and `schedule_tab.showDiff` translations |
| `apps/web/messages/en.json` | Same |
| `apps/web/messages/ru.json` | Same |

## Key decisions

1. **Client-side diff computation** — The API already returns assignments for any version. We fetch both the current and baseline versions and compute the diff in the browser. This avoids adding a new API endpoint.

2. **Three change types** — Added (green +), Removed (red −), Changed/swapped (amber ↔). "Changed" means the same time slot now has a different person.

3. **Clickable filter cards** — The summary cards (Added: 5, Removed: 2, Changed: 3) are clickable to filter the list. Click again to show all.

4. **Automatic baseline detection** — If the version has a `baselineVersionId`, use that. Otherwise, find the previous published version by version number.

5. **Handles "current" version** — The ScheduleTab passes `currentVersionId="current"` and the diff view fetches the current published version automatically.

6. **User-friendly display** — Each entry shows: task name, date/time, and the person change (with strikethrough for the old person and bold for the new one).

## UI design

```
┌─────────────────────────────────────────┐
│ שינויים בסידור                    [סגור] │
├─────────────────────────────────────────┤
│  ┌──────┐  ┌──────┐  ┌──────┐          │
│  │  5   │  │  2   │  │  3   │          │
│  │נוספו │  │הוסרו │  │הוחלפו│          │
│  └──────┘  └──────┘  └──────┘          │
├─────────────────────────────────────────┤
│ + שמירה │ ראשון 12 מאי 08:00–16:00     │
│   דני כהן                               │
│                                         │
│ ↔ שמירה │ שני 13 מאי 08:00–16:00       │
│   ̶י̶ו̶ס̶י̶ ̶ל̶ו̶י̶ → אבי ישראלי              │
│                                         │
│ − מטבח │ שלישי 14 מאי 12:00–14:00      │
│   רון דוד                               │
└─────────────────────────────────────────┘
```

## How it connects
- Uses the existing `getVersionDetail` API (step 011)
- Integrates into the ScheduleTab (step 086)
- Respects admin-only visibility (non-admins don't see the button)
- Works with the existing DiffSummaryCard component (step 083) — this is a more detailed view

## How to run / verify
1. Navigate to a group → Schedule tab (as admin)
2. Ensure at least 2 published versions exist (run solver twice)
3. Click the "שינויים" (Changes) button next to Export CSV
4. See the diff view with color-coded entries
5. Click the summary cards to filter by type
6. Click "סגור" to close

## What comes next
- Payment integration (Paddle) — when account is ready
- Crisp live chat — when Website ID is ready

## Git commit

```bash
git add -A && git commit -m "feat(phase8): schedule diff view — color-coded changes between schedule versions"
```
