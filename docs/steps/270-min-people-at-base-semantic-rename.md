# 270 — min_people_at_base Semantic Rename

## Phase

Home-Leave Overhaul — UX Improvement

## Purpose

Replaces the admin-facing concept of `leave_capacity` ("max people that can be home simultaneously") with `min_people_at_base` ("minimum people that must stay at base at all times"). This is more intuitive for military group admins — they think in terms of coverage requirements, not departure quotas. The solver still receives `leave_capacity` in its payload, computed as `memberCount - minPeopleAtBase`.

## What was built

| File | Change |
|------|--------|
| `infra/migrations/053_home_leave_overhaul.sql` | Added `min_people_at_base` column (INTEGER NOT NULL DEFAULT 8), CHECK constraint, and data migration |
| `Jobuler.Domain/Groups/HomeLeaveConfig.cs` | Changed default from 1 → 8 |
| `Jobuler.Application/HomeLeave/IOptimalRatioCalculator.cs` | Simplified signature: `Calculate(memberCount, minPeopleAtBase, leaveDurationHours)` |
| `Jobuler.Application/HomeLeave/OptimalRatioCalculator.cs` | Derives `leaveCapacity` and `coverageRequirement` internally from `minPeopleAtBase` |
| `Jobuler.Application/HomeLeave/IFeasibilityEngine.cs` | Simplified signature: `Evaluate(memberCount, minPeopleAtBase, baseDays, homeDays)` |
| `Jobuler.Application/HomeLeave/FeasibilityEngine.cs` | Derives `leaveCapacity` and `coverageRequirement` internally from `minPeopleAtBase` |
| `Jobuler.Application/HomeLeave/Commands/UpsertHomeLeaveConfigCommand.cs` | Default changed to 8; updated calculator/engine calls |
| `Jobuler.Application/HomeLeave/Queries/GetHomeLeaveConfigQuery.cs` | Default `MinPeopleAtBase` changed to 8 |
| `Jobuler.Api/Controllers/HomeLeaveConfigController.cs` | Simplified calls — no more manual `leaveCapacity`/`coverageRequirement` derivation in controller |
| `Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | `BuildHomeLeaveConfigDto` now accepts `memberCount`, computes `leaveCapacity = memberCount - minPeopleAtBase` |
| `Jobuler.Tests/HomeLeave/FeasibilityEngineTests.cs` | Rewritten to use new 4-parameter signature |
| `apps/web/components/home-leave/HomeLeaveConfigPanel.tsx` | Added "מינימום אנשים בבסיס" number input; removed `leaveCapacity` from save payload |
| `apps/web/messages/he.json` | Added `minPeopleAtBase` translations |
| `apps/web/messages/en.json` | Added `minPeopleAtBase` translations |
| `apps/web/messages/ru.json` | Added `minPeopleAtBase` translations |

## Key decisions

- **Default = 8**: Reasonable for a typical 14-person military group (leaves 6 slots for rotation).
- **Solver payload unchanged**: The Python solver still receives `leave_capacity` — no solver changes needed.
- **Backward compat**: `LeaveCapacity` column and property remain in the DB/entity; it's now computed server-side from `minPeopleAtBase` wherever needed.
- **Single source of truth**: The admin sets `minPeopleAtBase`; all other values are derived.

## How it connects

- The solver receives `leave_capacity` in its JSON payload (computed by `SolverPayloadNormalizer`).
- The frontend sends `minPeopleAtBase` to the API; the API computes `leaveCapacity` before storing.
- `OptimalRatioCalculator` and `FeasibilityEngine` now have simpler interfaces — callers just pass `minPeopleAtBase`.

## How to run / verify

```bash
cd apps/api && dotnet build --no-restore -v q
cd apps/web && npx tsc --noEmit
cd apps/api && dotnet test --no-build -v q
```

## What comes next

- Admin configures `min_people_at_base` in the UI per group.
- Consider adding a "recommended" badge based on group size.
- Eventually deprecate and drop the `leave_capacity` column once all clients use `min_people_at_base`.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): semantic rename leave_capacity → min_people_at_base"
```
