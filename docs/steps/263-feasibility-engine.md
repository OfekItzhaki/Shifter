# 263 — FeasibilityEngine Service

## Phase

Home-Leave Overhaul — Application Layer Services (Task 2.2)

## Purpose

Implements the `FeasibilityEngine` service that evaluates whether a given base:home day configuration can satisfy a group's coverage requirements. This is used by both Automatic and Manual modes to provide real-time feasibility feedback to admins. The engine is pure math (no DB calls), ensuring sub-500ms response times.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/IFeasibilityEngine.cs` | Interface defining the `Evaluate` method and `FeasibilityResult` record |
| `apps/api/Jobuler.Application/HomeLeave/FeasibilityEngine.cs` | Implementation with core feasibility check: `(memberCount - leaveCapacity) >= coverageRequirement` |
| `apps/api/Jobuler.Api/Program.cs` | DI registration as singleton |
| `apps/api/Jobuler.Tests/HomeLeave/FeasibilityEngineTests.cs` | 17 unit tests covering feasible/not-feasible scenarios, validation, and edge cases |

## Key decisions

- **Singleton registration** — The engine is stateless pure math, so singleton is appropriate (same as `OptimalRatioCalculator`).
- **Hebrew default reason** — When not feasible, returns a Hebrew reason string: "אין מספיק אנשים לכיסוי המשימות — יש להוסיף חברים או להקטין את מספר ימי הבית". Locale switching can be added later at the controller level.
- **MaxFeasibleHomeDays calculation** — When exactly at the coverage limit (surplus = 0), returns the current homeDays. When there's surplus, returns `max(homeDays, baseDays)` as a practical upper bound.
- **Input validation** — Throws `InvalidOperationException` for invalid inputs (memberCount < 1, leaveCapacity < 0, baseDays < 1, homeDays < 1, coverageRequirement < 1), consistent with the domain validation pattern.

## How it connects

- Depends on: Nothing (pure computation)
- Used by: `UpsertHomeLeaveConfigCommand` handler (task 4.1), `HomeLeaveConfigController` preview endpoint (task 4.5), frontend feasibility indicator
- Related: `OptimalRatioCalculator` (task 2.1) — both are stateless Application layer services

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~FeasibilityEngineTests"
```

All 17 tests pass.

## What comes next

- Task 2.3: Property test for Optimal Ratio Formula Correctness
- Task 2.4: Property test for Feasibility Engine Correctness
- Task 4.1: Update `UpsertHomeLeaveConfigCommand` to call `FeasibilityEngine`

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): implement FeasibilityEngine service with unit tests"
```
