# 391 — Auto-Resolve Recommendations on Manual Double-Shift Enable

## Phase

Feature: Double-Shift Recommendation — Application Layer Commands

## Purpose

When an admin manually enables `AllowsDoubleShift` on a task (via the existing task update flow), any active double-shift recommendations referencing that task become stale. This step adds auto-resolve logic so those recommendations are automatically marked as `Resolved`, keeping the recommendation UI clean and accurate.

## What was built

- **Modified:** `apps/api/Jobuler.Application/Tasks/Commands/GroupTaskCommands.cs`
  - Added `using Jobuler.Domain.Scheduling;` import for `RecommendationStatus` enum access
  - In `UpdateGroupTaskCommandHandler.Handle()`: captured the previous `AllowsDoubleShift` value before calling `task.Update()`
  - After the update, if `AllowsDoubleShift` changed from `false` to `true`, queries all active `DoubleShiftRecommendation` entities for that task and space, then calls `Resolve()` on each
  - All changes are persisted in a single `SaveChangesAsync` call (task update + recommendation resolves in one transaction)

## Key decisions

- **Detection approach:** Compare the entity's `AllowsDoubleShift` before and after the update command's value, rather than tracking via domain events. This is simpler and keeps the change localized to the handler.
- **Query scope:** Filters by both `GroupTaskId` and `SpaceId` to respect tenant isolation (security rule).
- **Single transaction:** Both the task update and recommendation resolves happen in one `SaveChangesAsync` call, ensuring atomicity.
- **Only active recommendations:** The query filters `Status == RecommendationStatus.Active` — dismissed/cleared/resolved recommendations are not affected.

## How it connects

- Implements **Requirement 5.2** from the double-shift-recommendation spec
- Uses the `DoubleShiftRecommendation.Resolve()` domain method (created in task 1.1)
- Uses `AppDbContext.DoubleShiftRecommendations` DbSet (created in task 2.1)
- Complements the `AcceptRecommendationCommand` (task 7.2) which also resolves recommendations but through the explicit accept flow

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

Build should succeed with no errors related to this change.

## What comes next

- Task 8.x: Query handlers that filter by active status will correctly exclude auto-resolved recommendations
- Property test 8.6 will validate this behavior: "Auto-resolved on manual enable"

## Git commit

```bash
git add -A && git commit -m "feat(double-shift-recommendation): auto-resolve recommendations on manual double-shift enable"
```
