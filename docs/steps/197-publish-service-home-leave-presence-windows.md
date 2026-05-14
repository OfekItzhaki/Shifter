# 197 — Publish Service: Home-Leave Presence Windows

## Phase

Phase 10 — Integration: Publish Service & Presence Windows

## Purpose

When a schedule version containing home-leave assignments is published, the system must create `PresenceWindow` records with `state = AtHome` and `is_derived = true`. This ensures the schedule timeline accurately reflects leave periods and prevents future solver runs from assigning missions during those windows.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/People/PresenceWindow.cs` | Added `CreateDerivedAtHome` factory method for creating derived AtHome presence windows |
| `apps/api/Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs` | Extended `PublishVersionCommandHandler` with `CreateHomeLeavePresenceWindowsAsync` method that reads home-leave assignments from SummaryJson, validates them, checks for on_mission overlaps, and creates presence windows |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs` | Extended `summaryJson` serialization to include `home_leave_assignments`, `home_leave_metrics`, and `fairness_variance` from solver output |

## Key decisions

| Decision | Rationale |
|----------|-----------|
| Store home-leave assignments in `SummaryJson` | The version's SummaryJson already stores solver output metadata. Adding home-leave data here avoids schema changes and keeps the data co-located with the version. |
| Validate at publish time, not at draft creation | Overlaps with on_mission windows may change between draft creation and publish. Validating at publish ensures correctness at the moment of commitment. |
| Throw `InvalidOperationException` on overlap | This maps to HTTP 400 via the existing `ExceptionHandlingMiddleware`, rejecting the publish cleanly. |
| Discard invalid entries with warnings | Unknown person_ids or invalid time ranges are logged but don't block the publish of valid entries. |
| New `CreateDerivedAtHome` factory method | Existing `CreateManual` sets `IsDerived = false` and `CreateDerived` only creates OnMission. A dedicated factory method keeps the domain model explicit. |

## How it connects

- **SolverWorkerService** (task 8.3) produces solver output with `home_leave_assignments` → now stored in `SummaryJson`
- **PublishVersionCommandHandler** reads these at publish time → creates `PresenceWindow` records
- **SolverPayloadNormalizer** (task 10.3) will read these `at_home` presence windows to prevent mission assignment during leave
- **Cancellation logic** (task 10.2) will delete/truncate these windows when leave is cancelled

## How to run / verify

```bash
# Build all projects
dotnet build apps/api/Jobuler.Api/Jobuler.Api.csproj

# Run existing publish tests
dotnet test apps/api/Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~PublishVersion"
```

## What comes next

- Task 10.2: Home-leave cancellation logic (truncate/delete presence windows)
- Task 10.3: Include published `at_home` windows in solver payload
- Property test 12 (presence window overlap detection on publish)

## Git commit

```bash
git add -A && git commit -m "feat(phase10): publish service creates AtHome presence windows from home-leave assignments"
```
