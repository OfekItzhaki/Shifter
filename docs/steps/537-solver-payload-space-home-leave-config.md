# Step 537 — Solver Payload: Space-Level Home-Leave Config Override

## Phase

Phase: Space Management — Infrastructure integration

## Purpose

When building the solver input payload for a closed-base group, the normalizer must prioritize space-level home-leave configuration over group-level configuration. This ensures that when a Space Owner sets home-leave parameters at the space level, those parameters are used consistently for all closed-base groups in the space.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverPayloadNormalizer.cs` | Added `using Jobuler.Domain.Spaces;` import. Modified `BuildAsync` to check `SpaceHomeLeaveConfigs` before falling back to group-level `HomeLeaveConfigs`. Added `BuildHomeLeaveConfigDto(SpaceHomeLeaveConfig, int)` overload. Updated `BuildPreviewAsync` fallback to also check space-level config first. |

## Key decisions

1. **Space-level config takes full precedence**: If a `SpaceHomeLeaveConfig` exists for the space and has `LeaveDurationHours > 0`, it is used entirely — no merging with group-level values.
2. **Fallback to group-level**: If no space-level config exists (or it has `LeaveDurationHours == 0`), the existing group-level `HomeLeaveConfig` logic is preserved unchanged.
3. **Separate overload rather than adapter**: A new `BuildHomeLeaveConfigDto(SpaceHomeLeaveConfig, int)` method was added rather than converting `SpaceHomeLeaveConfig` to `HomeLeaveConfig`. Both entities have identical fields, so the logic is duplicated but keeps the types explicit and avoids coupling.
4. **Preview path also updated**: `BuildPreviewAsync` has the same space-first fallback so preview mode is consistent with actual solver runs.

## How it connects

- Implements Requirements 6.3 and 6.5 from the space-management spec
- Depends on the `SpaceHomeLeaveConfig` entity (Task 1.3) and its EF configuration (Task 2.1)
- The `UpdateSpaceHomeLeaveConfigCommand` (Task 7.2) writes the config that this normalizer now reads
- Property test 13 (Task 10.3) will validate this behavior

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~SolverPayloadNormalizer"
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~SolverPayloadBurden"
```

All 17 existing solver payload tests should pass.

## What comes next

- Task 10.2: Audit logging for space management actions
- Task 10.3: Property tests for home-leave config propagation (Property 13) and audit logging (Property 12)

## Git commit

```bash
git add -A && git commit -m "feat(space-management): solver payload normalizer reads space-level home-leave config"
```
