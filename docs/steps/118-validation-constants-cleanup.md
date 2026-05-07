# Validation, Constants, and Code Quality Cleanup

## Title
Code quality improvements: TriggerMode validation, magic numbers → constants, SolverStartDateTime validation

## Phase
Phase 7 — Hardening

## Purpose
This step addresses three code quality issues identified during review:
1. TriggerMode enum validation was missing at the API boundary
2. Magic numbers (90 days, 86400000ms) were scattered in frontend code
3. SolverStartDateTime could be set to a past date without validation

## What was built

### 1. Frontend constants file
- Created `apps/web/lib/utils/constants.ts` with named constants:
  - `DEFAULT_TASK_HORIZON_DAYS = 90` — default task end horizon
  - `MS_PER_DAY = 86400000` — milliseconds in a day
  - `MIN_SOLVER_HORIZON_DAYS = 1` — minimum solver horizon
  - `MAX_SOLVER_HORIZON_DAYS = 90` — maximum solver horizon
  - `DEFAULT_SOLVER_HORIZON_DAYS = 7` — default solver horizon

### 2. Updated frontend files to use constants
- `apps/web/app/groups/[groupId]/page.tsx` — imported constants, replaced `90 * 86400000` with `DEFAULT_TASK_HORIZON_DAYS * MS_PER_DAY`
- `apps/web/components/ImportModal.tsx` — imported constants, replaced `90 * 86400000` with `DEFAULT_TASK_HORIZON_DAYS * MS_PER_DAY`

### 3. SolverStartDateTime past-date validation
- Updated `apps/api/Jobuler.Application/Groups/Commands/UpdateGroupSettingsCommand.cs`:
  - Added validation that rejects `SolverStartDateTime` values in the past
  - Throws `InvalidOperationException` with clear message if validation fails

### 4. TriggerMode validation (already implemented)
- `TriggerSolverCommandValidator` already validates TriggerMode is "standard" or "emergency"
- `ScheduleRunsController` already validates at API boundary with 400 response

## Key decisions
- Constants are centralized in a single file for easy maintenance
- Validation happens in the command handler (Application layer) per architecture rules
- Error message is in English (API layer) — translation happens in frontend

## How it connects
- Constants align with backend `SchedulingConstants.cs` for consistency
- Validation follows the same pattern as other domain validations in the codebase
- Part of the ongoing hardening phase to improve code quality

## How to run / verify
1. Build API: `cd apps/api && dotnet build`
2. Build frontend: `cd apps/web && npm run build`
3. Run unit tests: `cd apps/api && dotnet test --filter "FullyQualifiedName!~SolverEndToEndTests&FullyQualifiedName!~SolverWorkerPipelineTests"`
4. All 351 unit tests should pass

## What comes next
- Consider adding frontend validation for SolverStartDateTime to show immediate feedback
- Consider adding more property-based tests for the new validation

## Git commit
```bash
git add -A && git commit -m "fix(hardening): add validation and extract constants

- Add SolverStartDateTime past-date validation in UpdateGroupSettingsCommand
- Extract magic numbers to frontend constants file
- Replace 90 * 86400000 with named constants in page.tsx and ImportModal
- TriggerMode validation already implemented in command validator and controller"
```
