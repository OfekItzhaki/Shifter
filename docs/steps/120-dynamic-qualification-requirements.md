# Step 120 — Dynamic Qualification Requirements for Group Tasks

## Phase
Phase 4 — Solver Intelligence & Scheduling Constraints

## Purpose

Replaces the flat `required_qualification_names: string[]` on group tasks with a structured `qualification_requirements` list. Each entry specifies:

- **qualificationName** — which qualification is required
- **count** — how many seats per shift require this qualification
- **mandatory** — `true` = hard requirement (penalised heavily if unmet), `false` = soft preference (penalised lightly)

This enables the solver to enforce per-slot composition rules (e.g. "at least 1 Commander and 2 Medics per shift") rather than just a flat "someone must have X".

## What was built

### Database
- `infra/migrations/036_group_task_qualification_requirements.sql` — adds `qualification_requirements JSONB NOT NULL DEFAULT '[]'` to the `tasks` table. The old `required_qualification_names` column is kept for backward compat but is no longer written by the app.

### Domain
- `apps/api/Jobuler.Domain/Tasks/QualificationRequirement.cs` — new value object record `(string QualificationName, int Count, bool Mandatory)`.
- `apps/api/Jobuler.Domain/Tasks/GroupTask.cs` — updated:
  - Added `QualificationRequirements` property (persisted).
  - `RequiredQualificationNames` is now a computed property derived from mandatory requirements (backward compat).
  - `Create` and `Update` accept `List<QualificationRequirement>?` instead of `List<string>?`.

### Infrastructure
- `apps/api/Jobuler.Infrastructure/Persistence/Configurations/TasksConfiguration.cs` — maps `QualificationRequirements` to the `qualification_requirements` JSONB column with JSON serialization. `RequiredQualificationNames` is ignored by EF (it's computed).
- `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs`:
  - **UUID/name fix**: `TaskSlot.RequiredQualificationIds` (UUIDs) are now resolved to qualification names before being sent to the solver, matching `PersonEligibility.qualification_ids` which contains names.
  - Group task shift slots now include `qualification_requirements` in the solver payload.

### Application
- `apps/api/Jobuler.Application/Tasks/Commands/GroupTaskCommands.cs`:
  - Added `QualificationRequirementDto(string QualificationName, int Count, bool Mandatory)` record.
  - `GroupTaskDto` now returns `List<QualificationRequirementDto> QualificationRequirements` instead of `List<string> RequiredQualificationNames`.
  - `CreateGroupTaskCommand` and `UpdateGroupTaskCommand` accept `List<QualificationRequirementDto>?`.
  - Handlers map DTOs → domain objects before calling `GroupTask.Create` / `task.Update`.
- `apps/api/Jobuler.Application/Tasks/Queries/GetGroupTasksQuery.cs` — projection updated to map `QualificationRequirements`.
- `apps/api/Jobuler.Application/Scheduling/Models/SolverInputDto.cs` — added `QualificationRequirementSolverDto` record and `QualificationRequirements` optional field to `TaskSlotDto`.

### API
- `apps/api/Jobuler.Api/Controllers/TasksController.cs` — `CreateGroupTaskRequest` and `UpdateGroupTaskRequest` now use `List<QualificationRequirementDto>? QualificationRequirements`.

### Solver (Python)
- `apps/solver/models/solver_input.py` — added `QualificationRequirement` Pydantic model; `TaskSlot` now has `qualification_requirements: list[QualificationRequirement] = []`.
- `apps/solver/solver/constraints.py` — added `add_composition_constraints()` function that enforces per-slot qualification composition as soft penalties:
  - Mandatory requirements: penalty weight 500 per missing seat.
  - Optional requirements: penalty weight 50 per missing seat.
  - Never makes the model infeasible — always soft so partial schedules are still produced.
- `apps/solver/solver/engine.py` — imports and calls `add_composition_constraints` after availability constraints; composition penalty vars are added to the minimisation objective.

### Frontend
- `apps/web/lib/api/tasks.ts` — added `QualificationRequirementDto` interface; `GroupTaskDto` and `GroupTaskPayload` use `qualificationRequirements` instead of `requiredQualificationNames`.
- `apps/web/app/groups/[groupId]/tabs/TasksTab.tsx`:
  - `TaskForm` interface updated to `qualificationRequirements: Array<{qualificationName, count, mandatory}>`.
  - Task list display shows qualification requirements as colored badges (red = mandatory, violet = optional) with seat count.
  - Form replaced flat checkboxes with a dynamic composition builder: add/remove requirements, select qualification, set count, toggle mandatory/optional.
- `apps/web/app/groups/[groupId]/useGroupPageState.ts` — `DEFAULT_TASK_FORM` uses `qualificationRequirements: []`.
- `apps/web/app/groups/[groupId]/page.tsx` — `handleTaskSubmit` passes `qualificationRequirements`; `onEditTask` maps `t.qualificationRequirements ?? []`.
- `apps/web/components/ImportModal.tsx` — updated task import to use `qualificationRequirements: []`.

## Key decisions

1. **Soft-only composition constraints** — composition requirements are never hard constraints in the solver. This ensures the solver always produces a partial schedule rather than returning infeasible when qualified people are unavailable. The admin sees uncovered slots rather than a blank schedule.

2. **Backward compat via computed property** — `RequiredQualificationNames` on the domain entity is now derived from mandatory requirements. Any code that reads this property (e.g. old solver payloads, exports) continues to work without changes.

3. **Old DB column kept** — `required_qualification_names` stays in the DB. No data migration is needed; the new column starts empty and is populated on first save.

4. **UUID→name resolution in normalizer** — the existing `TaskSlot` path had a latent bug where qualification UUIDs were sent to the solver but `PersonEligibility.qualification_ids` contains names. Fixed in the same pass.

5. **Penalty weights** — mandatory: 500, optional: 50. Both are below the coverage weight (1000) so filling a slot always takes priority over composition.

## How it connects

- The solver's existing `add_qualification_constraints` (which blocks unqualified people from slots with `required_qualification_ids`) is kept for the legacy `TaskSlot` path.
- `add_composition_constraints` is the new mechanism for group task slots, operating on `qualification_requirements` rather than `required_qualification_ids`.
- The frontend composition builder feeds directly into the API payload, which persists to JSONB and is included in every solver run payload.

## How to run / verify

1. Apply the migration:
   ```powershell
   $env:PGPASSWORD="changeme_local"
   & "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U jobuler -d jobuler -f "infra/migrations/036_group_task_qualification_requirements.sql"
   ```

2. Build the API:
   ```bash
   cd apps/api && dotnet build
   ```

3. Build the frontend:
   ```bash
   cd apps/web && npm run build
   ```

4. Run solver tests:
   ```bash
   cd apps/solver && python -m pytest tests/ -v
   ```

5. Manual test: open a group → Tasks tab → create a task → add qualification requirements with count and mandatory/optional toggle → save → verify the task card shows the requirements as badges.

## What comes next

- Add validation: total `count` across all requirements should not exceed `requiredHeadcount`.
- Expose composition shortfalls in the solver output / hard conflicts explanation.
- Consider adding composition requirements to the task import CSV format.

## Git commit

```bash
git add -A && git commit -m "feat(tasks): dynamic qualification requirements with composition constraints"
```
