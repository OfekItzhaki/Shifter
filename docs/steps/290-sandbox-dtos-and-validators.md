# Step 290 — Sandbox DTOs and FluentValidation Validators

## Phase

Draft Simulation Sandbox — Backend Foundation

## Purpose

Defines the request DTOs and validation rules for the simulation sandbox feature. These DTOs are used by the simulation endpoint (`SimulateRequest`) and the publish-sandbox endpoint (`PublishSandboxRequest`, `TaskOverrideDto`, `ConstraintOverrideDto`, `SettingsOverrideDto`). All request bodies are validated before dispatching commands, following the project's input validation security rules.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Scheduling/Models/SimulateRequest.cs` | Request record for the stateless simulation endpoint; contains a full `SolverInputDto` payload. Validator ensures payload is present with required fields and non-negative headcounts. |
| `Jobuler.Application/Scheduling/Models/PublishSandboxRequest.cs` | Request record for publishing sandbox overrides; contains VersionId, task/constraint/member/settings overrides. Validator delegates to child validators. |
| `Jobuler.Application/Scheduling/Models/TaskOverrideDto.cs` | DTO for a single task override with action enum (add/edit/remove). Validator enforces action validity, required fields per action type, and field range checks. |
| `Jobuler.Application/Scheduling/Models/ConstraintOverrideDto.cs` | DTO for a single constraint override with action enum (add/edit/remove). Validator enforces action validity, required fields per action type, and severity enum. |
| `Jobuler.Application/Scheduling/Models/SettingsOverrideDto.cs` | DTO for settings overrides (rest hours, home-leave params, min people at base). Validator enforces range checks: 0–24 for rest hours, 0–100 for balance value, non-negative for others. |

## Key decisions

- **Records over classes**: Used C# records for immutability and value semantics, consistent with existing `SolverInputDto` pattern.
- **Action as string, not enum**: The `Action` field uses lowercase strings (`"add"`, `"edit"`, `"remove"`) validated via FluentValidation, matching the design doc's JSON contract and avoiding serialization complexity.
- **Conditional validation**: Fields required for "add" are not required for "remove" (only `ExistingTaskId`/`ExistingConstraintId` needed). This keeps the API ergonomic.
- **Settings range 0–24 hours**: `MinRestBetweenShiftsHours` validated to [0, 24] as specified in requirements 5.5.
- **Nullable fields**: All override fields are nullable — only provided values are applied during publish.

## How it connects

- `SimulateRequest` is consumed by `SimulationController.Simulate` (task 1.1)
- `PublishSandboxRequest` is consumed by `PublishSandboxCommand` (task 2.1) and the publish endpoint (task 2.2)
- Validators are auto-discovered by FluentValidation's assembly scanning (registered in DI via `AddValidatorsFromAssembly`)
- Frontend constructs these payloads from the Zustand sandbox store (tasks 4.1, 8.1)

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build
```

Build should succeed with no errors on the new files.

## What comes next

- Task 2.1: `PublishSandboxCommand` handler that accepts `PublishSandboxRequest`
- Task 2.2: Controller endpoint wiring with FluentValidation pipeline
- Task 1.1: `SimulationController` that accepts `SimulateRequest`

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): add DTOs and FluentValidation validators for simulation sandbox"
```
