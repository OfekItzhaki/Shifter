# 351 — Home Leave Presence Windows in Solver Payload

## Phase

Home Leave Protection — Solver Stability

## Purpose

Ensures the solver payload explicitly includes existing AtHome presence windows in the presence windows DTO, so the solver is aware of home leave as a constraint. This is critical for the emergency bypass scenario where people with AtHome windows remain in the solver pool and the solver must work around their leave periods rather than excluding them entirely.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Added explicit documentation comment on the presence windows section clarifying that ALL presence windows (including AtHome) are included for both excluded and non-excluded people, referencing Requirements 7.1, 7.2, 7.4 |

## Key decisions

1. **No filtering of presence windows by exclusion status**: AtHome windows are included in the presence DTO for ALL people (excluded and non-excluded). This ensures the solver has full visibility into home leave constraints regardless of the exclusion mode.

2. **Emergency bypass relies on presence windows as constraints**: When emergency bypass is active, people with AtHome windows stay in the solver pool. The solver uses their AtHome presence windows to avoid assigning them during leave periods, rather than excluding them entirely.

3. **Existing behavior was already correct**: The presence windows query fetches all states (FreeInBase, AtHome, OnMission) without filtering. The task was about making this intent explicit and documented for future maintainability.

## How it connects

- **Task 1.1 (Solver exclusion)**: Excludes people with AtHome windows from the people list in normal mode. This task ensures their windows are still sent for solver awareness.
- **Emergency bypass mode**: When `EmergencyFreezeActive && EmergencyUseForScheduling`, people stay in the pool and their AtHome windows serve as constraints.
- **Solver (Python CP-SAT)**: Receives `presence_windows` with `state: "at_home"` and treats them as blocked periods for assignment.

## How to run / verify

```bash
cd apps/api
dotnet test Jobuler.Tests --filter "FullyQualifiedName~SolverPayloadNormalizerTests"
dotnet test Jobuler.Tests --filter "FullyQualifiedName~Normalizer_BlockedPresenceWindow_MappedToCorrectStateString"
```

Both test suites should pass, confirming AtHome windows are correctly included in the solver payload with the `at_home` state string.

## What comes next

- Task 12: Final checkpoint — full integration verification of the home leave protection feature.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): document AtHome windows inclusion in solver presence DTO"
```
