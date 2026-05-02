# Handoff Document
_Last updated: May 1, 2026_

## Where we left off

The `schedule-table-autoschedule-role-constraints` spec is **fully complete** (all required tasks done). The branch is `feat/schedule-table-autoschedule-role-constraints`.

---

## Build status

| Layer | Command | Status |
|---|---|---|
| Frontend (Next.js) | `npx tsc --noEmit` (from `apps/web`) | ✅ 0 errors |
| .NET API | `dotnet build apps/api/Jobuler.Api/Jobuler.Api.csproj` | ✅ 0 errors, 0 warnings |
| Solver unit tests | `python -m pytest tests/ -q` (from `apps/solver`) | ✅ 41 passed |
| .NET unit tests | `dotnet test apps/api/Jobuler.Tests/Jobuler.Tests.csproj` | ✅ 287 passed |

> **Note on 12 failing .NET tests:** `SolverEndToEndTests` and `SolverWorkerPipelineTests` require the Python solver running on `localhost:8000`. They fail in any environment where the solver isn't running — this is expected and pre-existing. Start the solver with `uvicorn main:app --port 8000` from `apps/solver` to run them.

---

## What was built in this spec

All tasks 1–34 are complete (tasks 19–25 are optional `*` tests, not implemented for MVP).

### Summary of features shipped

1. **2D Schedule Table** (`ScheduleTable2D`) — tasks × time-slots grid, current-user column highlight, date filter, Hebrew empty state
2. **Auto-scheduler gap detection** — slot-level gap scan in `AutoSchedulerService`, triggers solver once per group when any slot is uncovered
3. **Role constraints** — `group_id` on `space_roles` and `person_role_assignments`, group-scoped role CRUD API + UI in SettingsTab
4. **Constraint scope expansion** — solver expands role/group constraints to per-person before solving
5. **Effective-date filtering** — constraints filtered by `effective_from`/`effective_until` against the horizon window
6. **Manual override assignments** — `ApplyManualOverrideCommand`, `RemoveManualOverrideCommand`, override modal in admin schedule page, locked slots in solver payload
7. **Live status panel** — polls `/live-status` every 30s, groups members by status with Hebrew labels, "סטטוס נוכחי" tab in GroupDetailPage

---

## Current branch

```
feat/schedule-table-autoschedule-role-constraints
```

Uncommitted files at handoff (all staged in the commit below):
- `apps/web/components/schedule/LiveStatusPanel.tsx` (new)
- `apps/web/components/schedule/OverrideModal.tsx` (new)
- `apps/api/Jobuler.Api/Controllers/LiveStatusController.cs` (new)
- `apps/api/Jobuler.Api/Controllers/ScheduleOverridesController.cs` (new)
- `apps/api/Jobuler.Application/Scheduling/Commands/ApplyManualOverrideCommand.cs` (new)
- `apps/api/Jobuler.Application/Scheduling/Queries/GetGroupLiveStatusQuery.cs` (new)
- Various modified files (solver, frontend pages, API client)

---

## How to pick this up on a new machine

```bash
# 1. Clone / pull
git clone https://github.com/OfekItzhaki/Shifter.git

# 2. Install PostgreSQL 16 locally (no Docker needed)
#    https://www.postgresql.org/download/windows/
#    Then create the DB:
#    CREATE USER jobuler WITH PASSWORD 'changeme_local';
#    CREATE DATABASE jobuler OWNER jobuler;

# 3. Run migrations (PowerShell)
$env:PGPASSWORD="changeme_local"
Get-ChildItem infra/migrations/*.sql | Sort-Object Name | ForEach-Object {
    psql -h localhost -U jobuler -d jobuler -f $_.FullName
}

# 4. Load seed data
psql -h localhost -U jobuler -d jobuler -f infra/scripts/seed.sql

# 5. Install frontend deps
cd apps/web && npm install --legacy-peer-deps

# 6. Install Python deps (solver)
cd apps/solver && pip install -r requirements.txt

# 7. Start services (3 terminals)
# Terminal 1 — API:
dotnet run --project apps/api/Jobuler.Api
# Terminal 2 — Solver:
cd apps/solver && uvicorn main:app --port 8000
# Terminal 3 — Frontend:
cd apps/web && npm run dev
```

> **Redis is optional** — the API falls back to an in-memory queue automatically.
> **Docker is optional** — only needed if you want containerised PostgreSQL/Redis.
> See `docs/LOCAL-SETUP.md` for full details.

---

## What's next (suggested)

- **Optional tests (tasks 19–25):** Property-based and unit tests for constraint validation, gap detection, group roles, solver expansion, and frontend components. All marked `*` (optional) in the spec.
- **PR / merge:** When ready, open a PR from `feat/schedule-table-autoschedule-role-constraints` → `main`.
- **Next spec:** Check `.kiro/specs/personal-and-role-constraints/requirements.md` — there's a related spec that may build on this work.

---

## Key files to know

| File | What it does |
|---|---|
| `apps/web/components/schedule/ScheduleTable2D.tsx` | 2D schedule grid component |
| `apps/web/components/schedule/LiveStatusPanel.tsx` | Live member status panel |
| `apps/web/components/schedule/OverrideModal.tsx` | Manual override modal |
| `apps/web/app/groups/[groupId]/page.tsx` | Group detail page (all tabs wired here) |
| `apps/api/Jobuler.Api/Controllers/LiveStatusController.cs` | Live status endpoint |
| `apps/api/Jobuler.Api/Controllers/ScheduleOverridesController.cs` | Manual override endpoints |
| `apps/api/Jobuler.Application/Scheduling/Queries/GetGroupLiveStatusQuery.cs` | Live status query handler |
| `apps/solver/solver/constraints.py` | Solver constraint functions incl. role/group expansion |
| `apps/solver/solver/engine.py` | Solver entry point — calls expand functions |
| `.kiro/specs/schedule-table-autoschedule-role-constraints/tasks.md` | Full task list with completion status |
| `docs/steps/` | Step-by-step build history (001–083) |
