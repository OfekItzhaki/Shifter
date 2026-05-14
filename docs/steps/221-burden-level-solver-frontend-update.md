# 221 — Burden Level Solver & Frontend Update

## Phase
Statistics Overhaul — Phase 1 (Burden Level Migration)

## Purpose
Complete the burden level taxonomy change across the solver and frontend layers. The domain enum was already renamed (step 219) and the database migration + EF Core config were updated (step 220). This step ensures the solver uses the new 3-level taxonomy with backward compatibility, and the frontend displays the new labels and colors.

## What was built

### Task 1.4: SolverPayloadNormalizer verification
- **No changes needed** — the normalizer already uses `.ToString().ToLower()` on the `TaskBurdenLevel` enum, which now produces "easy", "normal", "hard" automatically since the enum was renamed in step 219.

### Task 1.5: Python solver burden_map update
- **`apps/solver/solver/objectives.py`** — Updated the `burden_map` dictionary to include both new taxonomy keys ("hard"→4, "normal"→0, "easy"→-1) and legacy keys ("hated"→4, "disliked"→4, "neutral"→0, "favorable"→-1) for backward compatibility during transition. Added warning logging for unknown burden level strings that default to weight 0.

### Task 1.6: Frontend burden labels and colors
- **`apps/web/app/groups/[groupId]/types.ts`** — Replaced 4-level burden labels/colors with 3-level: hard→"קשה" (red), normal→"רגיל" (slate), easy→"קל" (emerald)
- **`apps/web/app/groups/[groupId]/tabs/TasksTab.tsx`** — Updated BURDEN_OPTIONS from 4 items to 3: ["easy", "normal", "hard"]
- **`apps/web/app/admin/tasks/page.tsx`** — Updated burden labels, colors, and dropdown to use new 3-level taxonomy
- **`apps/web/components/ImportModal.tsx`** — Updated BURDEN_MAP to map both new and legacy Hebrew/English terms to new taxonomy
- **`apps/web/components/ConstraintPayloadEditor.tsx`** — Updated no_consecutive_burden dropdown to use "hard", "normal", "easy"
- **`apps/web/app/groups/[groupId]/tabs/ConstraintsTab.tsx`** — Updated burden display map for constraint descriptions
- **`apps/web/app/groups/[groupId]/tabs/StatsTab.tsx`** — Updated "Hated Tasks" label to "Hard Tasks"
- **`apps/web/app/groups/[groupId]/page.tsx`** — Updated default constraint payload from "disliked" to "hard"
- **`apps/web/app/admin/stats/page.tsx`** — Updated leaderboard title from "Most Hated Tasks" to "Most Hard Tasks"
- **`apps/web/app/admin/constraints/page.tsx`** — Updated default constraint payload
- **`apps/web/lib/utils/groupTemplates.ts`** — Updated all template tasks from "hated"/"disliked" to "hard", "neutral" to "normal"
- **`apps/web/messages/en.json`** — Updated burden translation keys and stats labels
- **`apps/web/messages/he.json`** — Updated burden translation keys and stats labels
- **`apps/web/messages/ru.json`** — Updated burden translation keys and stats labels

## Key decisions
- **Backward compatibility in solver**: The burden_map keeps legacy keys so that during rolling deployment, payloads with old strings still work correctly.
- **Warning log for unknown levels**: Rather than crashing, unknown burden levels default to weight 0 (normal) with a warning — graceful degradation.
- **Both cases in frontend maps**: `burdenLabels` and `burdenColors` include both lowercase ("hard") and PascalCase ("Hard") keys to handle API responses that may use either casing.
- **Import modal maps legacy terms**: The Excel import BURDEN_MAP accepts both old Hebrew terms (שנוא, לא אהוב, נוח) and new ones (קשה, רגיל, קל) for backward compatibility with existing spreadsheets.

## How it connects
- Depends on step 219 (enum rename) and step 220 (migration + EF Core)
- The SolverPayloadNormalizer (verified in 1.4) emits the new strings that the solver (updated in 1.5) consumes
- The frontend (updated in 1.6) displays the new labels that match what the API returns
- Phase 2 (enhanced stats backend) will update the stats API response fields to use the new naming

## How to run / verify
1. **Solver**: Run `python -c "from solver.objectives import *; print('OK')"` in the solver directory to verify no import errors
2. **Frontend**: Run `npm run build` in `apps/web` to verify TypeScript compilation
3. **Visual**: Navigate to any group's Tasks tab — the burden dropdown should show 3 options (קל, רגיל, קשה)
4. **Constraint editor**: Create a "no_consecutive_burden" constraint — dropdown should show Hard/Normal/Easy

## What comes next
- Phase 1 checkpoint (task 2): verify all tests pass and migration runs cleanly
- Phase 2 (task 3.x): expand FairnessCounter entity, create historical snapshots table, update stats API

## Git commit
```bash
git add -A && git commit -m "feat(statistics-overhaul): update solver burden_map and frontend labels to 3-level taxonomy"
```
