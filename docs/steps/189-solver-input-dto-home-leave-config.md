# 189 — SolverInputDto: HomeLeaveConfigDto Extension

## Phase

Phase 3 — API Backend: Solver Payload Extensions (Home-Leave Scheduling)

## Purpose

Extends the .NET `SolverInputDto` record with a `HomeLeaveConfigDto` record so the API can serialize home-leave configuration into the solver payload JSON. This is the C# counterpart to the Python `HomeLeaveConfig` model added in step 187.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Models/SolverInputDto.cs` | Added `HomeLeaveConfigDto` record with fields: `Enabled` (bool), `MinRestHours` (double), `EligibilityThresholdHours` (double), `LeaveCapacity` (int), `LeaveDurationHours` (double). All fields use `[JsonPropertyName]` attributes with snake_case names. Added optional `HomeLeaveConfig` parameter (nullable, default null) as the last parameter of `SolverInputDto`. |

## Key decisions

- Used `[property: JsonPropertyName(...)]` attributes on `HomeLeaveConfigDto` fields for explicit snake_case serialization, consistent with other nested records like `StabilityWeightsDto` and `QualificationRequirementSolverDto`.
- The `HomeLeaveConfig` property on `SolverInputDto` itself does not need a `[JsonPropertyName]` attribute because the `SolverHttpClient` uses `JsonNamingPolicy.SnakeCaseLower` globally, which converts `HomeLeaveConfig` → `home_leave_config` automatically.
- Placed `HomeLeaveConfigDto` at the end of the file, after `FairnessCountersDto`, following the existing pattern of declaring records in dependency order.
- Used `double` for hours fields and `int` for capacity, matching the Python model's `float`/`int` types.

## How it connects

- `SolverPayloadNormalizer.BuildAsync` (Task 5.3) will construct a `HomeLeaveConfigDto` instance from the group's `HomeLeaveConfig` domain entity and pass it to `SolverInputDto`.
- The `SolverHttpClient` serializes `SolverInputDto` to JSON with `SnakeCaseLower` policy, producing the `home_leave_config` field expected by the Python solver.
- The Python solver's `HomeLeaveConfig` Pydantic model (step 187) deserializes this field.

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

All projects (Domain, Application, Infrastructure, Api, Tests) compile with 0 warnings and 0 errors.

## What comes next

- Task 5.2: Extend `SolverOutputDto` with home-leave assignment and metrics fields.
- Task 5.3: Extend `SolverPayloadNormalizer` to populate `HomeLeaveConfigDto` for closed-base groups.

## Git commit

```bash
git add -A && git commit -m "feat(api): add HomeLeaveConfigDto to SolverInputDto"
```
