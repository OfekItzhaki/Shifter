# 346 — Solver Home-Leave Exclusion

## Phase

Home Leave Protection — Solver Exclusion

## Purpose

Prevents the solver from assigning tasks to people who have active or future home leave (AtHome presence windows) overlapping the solver horizon. This ensures that approved home leave is never silently overridden by automated scheduling.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Added `GetExcludedPersonIdsAsync` private method, emergency bypass flag extraction, people/baseline filtering |

### Key changes in `SolverPayloadNormalizer.cs`:

1. **`GetExcludedPersonIdsAsync` method** — Queries `PresenceWindows` for AtHome windows overlapping the solver horizon (`StartsAt < horizonEnd AND EndsAt > horizonStart`). Returns a `HashSet<Guid>` of person IDs to exclude. Returns empty set when `emergencyBypass` is true.

2. **Emergency bypass flag** — Extracted from `HomeLeaveConfig` when `EmergencyFreezeActive && EmergencyUseForScheduling` is true. Stored in a `bool emergencyBypass` variable scoped to the full `BuildAsync` method.

3. **People filtering** — After building `peopleDto`, excluded person IDs are removed from the list so the solver never sees them.

4. **Baseline assignment filtering** — Baseline assignments referencing excluded people are removed, preventing the solver from trying to maintain assignments for people who are on leave.

5. **Locked slot filtering** — Locked slots (manual overrides) for excluded people are also removed since those people can't be assigned anyway.

## Key decisions

- **Exclusion at payload-build time**: Filtering happens in the normalizer before the payload is sent to the solver, keeping the solver stateless and unaware of home-leave logic.
- **Emergency bypass granularity**: Only bypasses when BOTH `EmergencyFreezeActive` AND `EmergencyUseForScheduling` are true. If freeze is active but not used for scheduling, the config is omitted entirely (existing behavior) but exclusion still applies.
- **Overlap semantics**: Uses strict overlap check (`StartsAt < horizonEnd AND EndsAt > horizonStart`) which catches both active windows (started before now) and future windows (starting after now but before horizon end).

## How it connects

- Implements Requirements 1.1, 1.2, 1.3, 1.4 from the Home Leave Protection spec
- Uses the existing `PresenceWindow` domain entity and `PresenceState.AtHome` enum
- Integrates with the existing `HomeLeaveConfig` emergency freeze mechanism
- The solver receives a clean payload with no references to excluded people

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~SolverPayloadNormalizer"
```

All 11 existing normalizer tests should pass.

## What comes next

- Task 1.2: Property test for solver exclusion completeness (FsCheck)
- Task 1.3: Property test for emergency bypass
- Task 2.1: Publish-time protection of existing AtHome windows

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-protection): solver exclusion of people on home leave"
```
