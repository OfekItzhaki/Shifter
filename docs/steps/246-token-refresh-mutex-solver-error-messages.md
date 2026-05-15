# 246 — Solver Progress Visibility & Timeout Fix

## Phase
Production Bug Fixes + UX Improvement

## Purpose
1. Fixes the solver timeout: CP-SAT was set to 60s in production but HTTP timeout was 20s — the solver was working but the API gave up
2. Adds live progress phases so the user sees what the solver is doing (no more mystery spinner)
3. Optimizes constraint building (pre-computed timestamps) to eliminate ~500K ISO string parses
4. Allows 1-day planning horizon (slider min changed from 3 to 1)
5. Removes dead code (`_slots_overlap` function)

## What was built

### Backend
- **`Jobuler.Domain/Scheduling/ScheduleRun.cs`** — Added `ProgressPhase` property and `SetProgressPhase()` method
- **`infra/migrations/051_schedule_run_progress_phase.sql`** — DB migration for the new column
- **`Jobuler.Application/Scheduling/Queries/GetScheduleRunQuery.cs`** — Returns `progressPhase` in poll response
- **`Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs`** — Sets progress phases: `building_payload` → `solving` → `storing_results`
- **`Jobuler.Api/Program.cs`** — HTTP timeout increased to 90s (solver can now run up to 60s)
- **`.env` / `.env.example`** — `SOLVER_TIMEOUT_SECONDS=60` (was 60 but HTTP was 20s — now they're aligned)

### Solver (Python)
- **`solver/constraints.py`** — Pre-compute timestamps once before O(n²) loops. Removed dead `_slots_overlap` function.
- **`solver/engine.py`** — Added timing instrumentation (logs constraint build time vs solve time)

### Frontend
- **`app/groups/[groupId]/page.tsx`** — Removed 30s hard polling timeout. Polls until terminal. Shows progress phase.
- **`app/groups/[groupId]/tabs/SettingsTab.tsx`** — Button shows current phase ("בונה נתונים...", "מחפש פתרון...", "שומר תוצאות..."). Slider min changed to 1.
- **`messages/he.json`** / **`messages/en.json`** — Added solver phase translations and common completed/timedOut keys

## Key decisions
- Solver can now run up to 60s because the user sees live progress — no more mystery timeout
- HTTP timeout is 90s to give breathing room beyond the 60s CP-SAT limit
- Frontend no longer has a hard polling timeout — it polls until the backend reports a terminal status
- Progress phases are stored in the DB so they survive page refreshes

## How to run / verify
1. Run migration `051_schedule_run_progress_phase.sql`
2. Rebuild and deploy all 3 containers (API, solver, web)
3. Trigger the solver — button should show "בונה את הנתונים..." then "מחפש פתרון אופטימלי..." then "שומר תוצאות..."
4. Solver should complete successfully (no more timeout)

## Git commit
```bash
git add -A && git commit -m "feat(solver): live progress phases + timeout fix + perf optimization"
```
