# 385 — Recommendation Query DTOs

## Phase

Double-Shift Recommendation — Application Layer

## Purpose

Define the response DTOs used by query handlers to return recommendation data to the API layer. These records provide a clean, immutable contract between the application and API layers for the double-shift recommendation feature.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/Models/RecommendationDto.cs` | Record representing a single recommendation with all fields needed for display (Id, GroupTaskId, TaskName, Status, AdditionalSlotsCovered, AffectedDateStart, AffectedDateEnd, TotalUncoveredSlotsInRun, CreatedAt) |
| `apps/api/Jobuler.Application/Scheduling/Models/RecommendationBannerDto.cs` | Aggregated record for the solver results banner — includes TotalUncoveredSlots, a capped list of Recommendations (max 5), RemainingCount for "+N more" display, and AffectedDateRange as a formatted string |

## Key decisions

- Used C# positional records for immutability, matching the existing DTO style in the project (e.g., `CumulativeTrackingDto`, `ConstraintOverrideDto`).
- `Status` is typed as `string` in the DTO (not the enum) to keep the Application layer decoupled from serialization concerns — the mapping from `RecommendationStatus` enum happens in the query handler.
- `AffectedDateRange` in `RecommendationBannerDto` is a pre-formatted string so the frontend doesn't need to compute the range from individual recommendations.
- `Recommendations` in the banner DTO is capped at 5 items by the query handler, with `RemainingCount` indicating how many more exist.

## How it connects

- These DTOs are returned by the query handlers (`GetActiveRecommendationsQuery`, `GetRecommendationsForRunQuery`, `GetRecommendationForTaskQuery`) defined in task 8.
- The `RecommendationsController` (task 10) serializes these DTOs as JSON responses.
- The frontend TypeScript types (task 12.1) mirror these shapes exactly.
- Maps from the `DoubleShiftRecommendation` domain entity created in task 1.1.

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore --verbosity quiet
```

Build should succeed with no errors related to the new files.

## What comes next

- Task 5.1: Implement `RecommendationEngine` that produces `RecommendationItem` records
- Task 8.x: Query handlers that map domain entities to these DTOs
- Task 10.x: API controller endpoints that return these DTOs

## Git commit

```bash
git add -A && git commit -m "feat(double-shift): add RecommendationDto and RecommendationBannerDto query DTOs"
```
