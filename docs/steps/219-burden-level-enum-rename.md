# 219 — Burden Level Enum Rename

## Phase

Statistics Overhaul — Phase 1 (Task 1.1)

## Purpose

Replace the 4-level `TaskBurdenLevel` enum (`Favorable`, `Neutral`, `Disliked`, `Hated`) with a simpler 3-level model (`Easy`, `Normal`, `Hard`). This reduces cognitive load and aligns with the new statistics system design where "Hard" captures both old negative levels.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Domain/Tasks/TaskBurdenLevel.cs` | Enum values changed to `Easy`, `Normal`, `Hard` |
| `Jobuler.Domain/Tasks/GroupTask.cs` | Default `BurdenLevel` changed from `Neutral` to `Normal` |
| `Jobuler.Domain/Tasks/TaskType.cs` | Default `BurdenLevel` changed from `Neutral` to `Normal` |
| `Jobuler.Application/Tasks/Commands/GroupTaskCommands.cs` | Validators updated to accept `["easy", "normal", "hard"]` |
| `Jobuler.Application/Tasks/Validators/CreateTaskTypeCommandValidator.cs` | Valid levels updated to `["Easy", "Normal", "Hard"]` |
| `Jobuler.Application/Scheduling/Commands/UpdateFairnessCountersCommand.cs` | Burden level checks updated from `Hated`/`Disliked` to `Hard` |
| `Jobuler.Application/Scheduling/Queries/GetBurdenStatsQuery.cs` | String comparisons updated from `"hated"`/`"disliked"`/`"favorable"` to `"hard"`/`"easy"` |
| `Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Default fallback changed from `Neutral` to `Normal` |
| `Jobuler.Tests/Domain/GroupTaskTests.cs` | Test data updated to new enum values |
| `Jobuler.Tests/Application/GroupTaskPropertyTests.cs` | InlineData and string references updated |
| `Jobuler.Tests/Application/AdminManagementHandlerTests.cs` | Valid burden levels updated |
| `Jobuler.Tests/Application/SolverEndToEndTests.cs` | Burden level strings updated |
| `Jobuler.Tests/Integration/AdminManagementIntegrationTests.cs` | Enum and string references updated |
| `Jobuler.Tests/Integration/SolverWorkerPipelineTests.cs` | Enum references updated |
| `Jobuler.Tests/Scheduling/AutoSchedulerBugConditionTests.cs` | Enum references updated |

## Key decisions

- **Mapping**: `Hated` + `Disliked` → `Hard`, `Neutral` → `Normal`, `Favorable` → `Easy`
- **Validators**: Now only accept the 3 new values; old values are rejected at the API boundary
- **FairnessCounter entity**: Property names (`HatedTasks7d`, etc.) left unchanged for now — they'll be renamed in the database migration (task 1.2) and entity expansion (task 3.1)
- **GetBurdenStatsQuery DTO**: Field names like `HatedTasksAllTime` kept for backward compatibility until the full stats overhaul replaces them

## How it connects

- **Task 1.2** (database migration) will rename the stored values in PostgreSQL
- **Task 1.3** (EF Core config) will update string conversion mappings
- **Task 1.4** (SolverPayloadNormalizer) will ensure the solver receives new strings
- **Task 1.5** (Python solver) will add backward-compatible burden_map entries
- **Task 1.6** (frontend) will update UI labels and colors

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore   # 0 errors, 0 warnings
dotnet test --no-build --filter "FullyQualifiedName~GroupTaskTests|FullyQualifiedName~GroupTaskPropertyTests|FullyQualifiedName~AdminManagement"
```

All 96 tests pass. Full suite: 452 pass, 12 skip (SolverEndToEndTests require running solver service).

## What comes next

- Task 1.2: Database migration to rename stored burden level values
- Task 1.3: EF Core enum conversion configuration update

## Git commit

```bash
git add -A && git commit -m "feat(statistics): rename TaskBurdenLevel enum from 4-level to 3-level taxonomy"
```
