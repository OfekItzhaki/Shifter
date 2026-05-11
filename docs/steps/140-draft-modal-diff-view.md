# 140 — Draft Modal Diff View

## Phase
Phase 8 — UX

## Purpose
When reviewing a draft schedule before publishing, admins want to see what changed compared to the current published version. This adds a "Changes" tab to the draft modal that reuses the ScheduleDiffView component.

## What was built

### Files modified:

| File | Change |
|------|--------|
| `apps/web/components/DraftScheduleModal.tsx` | Added ScheduleDiffView import, view mode toggle (Schedule/Changes), renders diff when "Changes" tab is selected |

## Key decisions

1. **Reuse existing component** — The ScheduleDiffView from step 138 is rendered directly inside the draft modal. No new component needed.

2. **Tab toggle** — A small pill toggle at the top of the modal body lets admins switch between "Schedule" (the full draft table) and "Changes" (the diff view).

3. **Compares draft vs current** — The diff view receives the `draftVersionId` and automatically finds the current published version as the baseline.

## How to run / verify
1. Run the solver to create a draft
2. Open the draft modal (click "View Draft")
3. See the toggle at the top: "לוח זמנים" | "שינויים בסידור"
4. Click "שינויים בסידור" to see what changed vs the current published schedule

## Git commit

```bash
git add -A && git commit -m "feat(phase8): dark mode toggle + diff view in draft modal"
```
