# Implementation Plan: Double-Shift Recommendation

## Overview

This plan implements a post-solve recommendation engine that detects staffing shortfalls and suggests enabling `AllowsDoubleShift` on specific tasks to improve coverage. The engine hooks into `SolverWorkerService` after each solver run, analyzes uncovered slots, and produces ranked recommendations stored as a new domain entity. Recommendations surface via notifications, an inline banner on solver results, and inline suggestions in task settings.

## Tasks

- [x] 1. Domain layer — Entity and enums
  - [x] 1.1 Create `DoubleShiftRecommendation` entity and `RecommendationStatus` enum
    - Add `RecommendationStatus` enum (`Active`, `Dismissed`, `Resolved`, `Cleared`) in `Jobuler.Domain/Scheduling/`
    - Add `DoubleShiftRecommendation` entity class implementing `Entity` and `ITenantScoped`
    - Include all properties: `SpaceId`, `GroupId`, `ScheduleRunId`, `GroupTaskId`, `TaskName`, `Status`, `AdditionalSlotsCovered`, `AffectedDateStart`, `AffectedDateEnd`, `TotalUncoveredSlotsInRun`, `DismissedAt`, `DismissedByUserId`, `ResolvedAt`, `ClearedAt`
    - Add domain methods for lifecycle transitions: `Dismiss(userId)`, `Resolve()`, `Clear()`
    - _Requirements: 5.1, 5.2, 5.3, 5.5_

- [x] 2. Infrastructure — Database configuration and migration
  - [x] 2.1 Add EF Core entity configuration for `DoubleShiftRecommendation`
    - Create `DoubleShiftRecommendationConfiguration.cs` in `Jobuler.Infrastructure/Persistence/Configurations/`
    - Map to table `double_shift_recommendations`
    - Configure `Status` as string conversion with max length 20
    - Add composite indexes: `ix_dsr_space_group_status`, `ix_dsr_space_run`, `ix_dsr_space_task_status`, `ix_dsr_created_at`
    - Add `DbSet<DoubleShiftRecommendation>` to `AppDbContext`
    - _Requirements: 5.3_

  - [x] 2.2 Create database migration for `double_shift_recommendations` table
    - Generate EF Core migration with all columns, constraints, and indexes
    - Include RLS policy `dsr_tenant_isolation` using `space_id = current_setting('app.current_space_id')::uuid`
    - Add unique constraint on (`space_id`, `schedule_run_id`, `group_task_id`) for upsert pattern
    - _Requirements: 5.3_

- [x] 3. Application layer — Interface, DTOs, and result types
  - [x] 3.1 Create `IRecommendationEngine` interface and result records
    - Add `IRecommendationEngine.cs` in `Jobuler.Application/Scheduling/`
    - Define `AnalyzeAsync(spaceId, groupId, runId, input, output, ct)` method
    - Add `RecommendationResult` record with `HasShortfall` and `Recommendations` list
    - Add `RecommendationItem` record with `GroupTaskId`, `TaskName`, `AdditionalSlotsCovered`, `AffectedDateStart`, `AffectedDateEnd`
    - _Requirements: 1.1, 2.1_

  - [x] 3.2 Create query DTOs for recommendation responses
    - Add `RecommendationDto` record in `Jobuler.Application/Scheduling/Models/`
    - Include fields: `Id`, `GroupTaskId`, `TaskName`, `Status`, `AdditionalSlotsCovered`, `AffectedDateStart`, `AffectedDateEnd`, `TotalUncoveredSlotsInRun`, `CreatedAt`
    - Add `RecommendationBannerDto` with `TotalUncoveredSlots`, `Recommendations` (capped at 5), `RemainingCount`, `AffectedDateRange`
    - _Requirements: 3.1, 3.2, 3.3_

- [x] 4. Checkpoint — Domain and application layer compilation
  - Ensure domain entity, enums, interface, and DTOs compile without errors.
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Infrastructure — Recommendation engine implementation
  - [x] 5.1 Implement `RecommendationEngine` core analysis logic
    - Create `RecommendationEngine.cs` in `Jobuler.Infrastructure/Scheduling/`
    - Implement `IRecommendationEngine.AnalyzeAsync`
    - Step 1: Shortfall detection — calculate available personnel per day (total members − home leave assignments), flag days where available < `MinPeopleAtBase`
    - Step 2: Candidate filtering — select active `GroupTask` entities where `AllowsDoubleShift == false`, skip if fewer than 2 candidates
    - Step 3: Coverage simulation — for each candidate, count uncovered slots and calculate how many consecutive pairs could be filled by allowing double shifts
    - Step 4: Ranking — sort by `AdditionalSlotsCovered` DESC, then `TaskName` ASC, cap at 10
    - Step 5: Return `RecommendationResult`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.4, 2.5, 6.4_

  - [x] 5.2 Implement recommendation persistence and lifecycle management
    - After analysis, insert `DoubleShiftRecommendation` rows with status `Active`
    - If no shortfall detected, transition all existing `Active` recommendations for the group to `Cleared`
    - Use upsert pattern on (`space_id`, `schedule_run_id`, `group_task_id`) to handle re-runs
    - Check `EmergencyFreezeActive` on `HomeLeaveConfig` — skip generation if true
    - _Requirements: 5.1, 5.4, 6.3_

  - [x] 5.3 Integrate recommendation engine into `SolverWorkerService`
    - Inject `IRecommendationEngine` into `SolverWorkerService` constructor
    - Call `AnalyzeAsync` at the end of `ProcessNextJobAsync`, after run is marked completed and before notification dispatch
    - Wrap in try/catch — log warning on failure, never disrupt solver flow
    - If recommendations produced, call `INotificationService.NotifySpaceAdminsAsync` with event type `double_shift_recommendation`
    - Skip notification if no admins exist in the group
    - _Requirements: 1.1, 3.1, 3.4_

  - [ ]* 5.4 Write property test: Staffing shortfall detection accuracy
    - **Property 1: Staffing shortfall detection accuracy**
    - **Validates: Requirements 1.2**
    - Generate random group sizes, home leave assignment sets, and MinPeopleAtBase thresholds
    - Assert shortfall flagged if and only if (total − on_leave) < MinPeopleAtBase for any day

  - [ ]* 5.5 Write property test: Engine only evaluates eligible tasks
    - **Property 2: Engine only evaluates eligible tasks**
    - **Validates: Requirements 1.3, 6.2**
    - Generate random GroupTask collections with mixed `AllowsDoubleShift`/`IsActive` states
    - Assert no recommendation references a task where `AllowsDoubleShift == true` or `IsActive == false`

  - [ ]* 5.6 Write property test: Engine soundness — every recommendation reduces uncovered slots
    - **Property 3: Engine soundness — every recommendation reduces uncovered slots**
    - **Validates: Requirements 1.1, 2.1**
    - Generate random solver outputs with uncovered slots and candidate tasks
    - Assert every recommendation has `AdditionalSlotsCovered >= 1`

  - [ ]* 5.7 Write property test: Simulation accuracy — additional slots calculation
    - **Property 4: Simulation accuracy — additional slots calculation**
    - **Validates: Requirements 1.4**
    - Generate random uncovered slot patterns for candidate tasks
    - Assert calculated `AdditionalSlotsCovered` equals the number of consecutive uncovered slot pairs

  - [ ]* 5.8 Write property test: Ranking correctness
    - **Property 5: Ranking correctness**
    - **Validates: Requirements 2.2**
    - Generate random recommendation lists with varying slot counts and task names
    - Assert list is sorted DESC by `AdditionalSlotsCovered`, then ASC by `TaskName`

  - [ ]* 5.9 Write property test: Recommendation structure completeness
    - **Property 6: Recommendation structure completeness**
    - **Validates: Requirements 2.3**
    - Generate random valid engine inputs
    - Assert every recommendation has non-empty `TaskName`, positive `AdditionalSlotsCovered`, and `AffectedDateStart <= AffectedDateEnd`

  - [ ]* 5.10 Write property test: Maximum 10 recommendations cap
    - **Property 7: Maximum 10 recommendations cap**
    - **Validates: Requirements 2.5**
    - Generate random inputs with more than 10 eligible tasks
    - Assert result list has at most 10 items and contains the top 10 by ranking

- [x] 6. Checkpoint — Engine implementation and property tests
  - Ensure recommendation engine compiles and integrates with SolverWorkerService.
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Application layer — Commands
  - [x] 7.1 Implement `DismissRecommendationCommand` handler
    - Create `DismissRecommendationCommand.cs` in `Jobuler.Application/Scheduling/Commands/`
    - Accept `SpaceId`, `RecommendationId`, `UserId`
    - Verify recommendation exists and belongs to the space
    - Call `IPermissionService.RequirePermissionAsync` for `ViewAndEdit` or `Owner`
    - Call `Dismiss(userId)` on the entity, persist changes
    - _Requirements: 4.3, 4.4, 6.1_

  - [x] 7.2 Implement `AcceptRecommendationCommand` handler
    - Create `AcceptRecommendationCommand.cs` in `Jobuler.Application/Scheduling/Commands/`
    - Accept `SpaceId`, `RecommendationId`, `UserId`, `TriggerNewRun`
    - Verify recommendation exists and belongs to the space
    - Call `IPermissionService.RequirePermissionAsync` for `ViewAndEdit` or `Owner`
    - Load the referenced `GroupTask` — if task not found, mark recommendation as `Cleared` and return 404
    - If `AllowsDoubleShift` is already true, return informational message and mark as `Resolved`
    - Otherwise, set `AllowsDoubleShift = true` on the task, mark recommendation as `Resolved`
    - If `TriggerNewRun` is true, enqueue a new solver run for the group
    - _Requirements: 4.1, 4.2, 4.5, 5.2_

  - [x] 7.3 Implement auto-resolve on manual double-shift enable
    - In the existing task update command/handler, add logic to detect when `AllowsDoubleShift` changes from false to true
    - Query active recommendations referencing that `GroupTaskId` and mark them as `Resolved`
    - _Requirements: 5.2_

- [x] 8. Application layer — Queries
  - [x] 8.1 Implement `GetActiveRecommendationsQuery` handler
    - Create `GetActiveRecommendationsQuery.cs` in `Jobuler.Application/Scheduling/Queries/`
    - Accept `SpaceId`, `GroupId`, `UserId`
    - Check user permission level — return empty if not `ViewAndEdit` or `Owner`
    - Check `EmergencyFreezeActive` on `HomeLeaveConfig` — return empty if true
    - Query `DoubleShiftRecommendation` where `Status == Active`, filter by space and group
    - Map to `RecommendationDto` list
    - _Requirements: 4.4, 6.1, 6.3, 6.5_

  - [x] 8.2 Implement `GetRecommendationsForRunQuery` handler
    - Create `GetRecommendationsForRunQuery.cs` in `Jobuler.Application/Scheduling/Queries/`
    - Accept `SpaceId`, `RunId`, `UserId`
    - Check user permission level — return empty if not `ViewAndEdit` or `Owner`
    - Check `EmergencyFreezeActive` — return empty if true
    - Query recommendations for the specific run, return as `RecommendationBannerDto` (cap task names at 5)
    - _Requirements: 3.2, 6.1, 6.3_

  - [x] 8.3 Implement `GetRecommendationForTaskQuery` handler
    - Create `GetRecommendationForTaskQuery.cs` in `Jobuler.Application/Scheduling/Queries/`
    - Accept `SpaceId`, `GroupTaskId`, `UserId`
    - Check user permission level — return empty if not `ViewAndEdit` or `Owner`
    - Check `EmergencyFreezeActive` — return empty if true
    - Query active recommendation for the specific task
    - Return single `RecommendationDto` or null
    - _Requirements: 3.3, 6.1, 6.2, 6.3_

  - [ ]* 8.4 Write property test: Active status filter correctness
    - **Property 8: Active status filter correctness**
    - **Validates: Requirements 4.4, 5.5**
    - Generate random sets of recommendations with mixed statuses
    - Assert query returns only those with status `Active`

  - [ ]* 8.5 Write property test: Cleared on successful run
    - **Property 9: Cleared on successful run**
    - **Validates: Requirements 5.1**
    - Generate groups with active recommendations and a new run without shortfall
    - Assert all previously-active recommendations transition to `Cleared`, other statuses unchanged

  - [ ]* 8.6 Write property test: Auto-resolved on manual enable
    - **Property 10: Auto-resolved on manual enable**
    - **Validates: Requirements 5.2**
    - Generate tasks with active recommendations, then enable `AllowsDoubleShift`
    - Assert all active recommendations for that task become `Resolved`, others unchanged

  - [ ]* 8.7 Write property test: Fresh generation ignores prior dismissals
    - **Property 11: Fresh generation ignores prior dismissals**
    - **Validates: Requirements 5.4**
    - Generate previously dismissed recommendations and a new shortfall run
    - Assert engine evaluates all eligible tasks regardless of prior dismissals

  - [ ]* 8.8 Write property test: Permission-based visibility
    - **Property 12: Permission-based visibility**
    - **Validates: Requirements 6.1, 6.5**
    - Generate users with varying permission levels
    - Assert only `ViewAndEdit` or `Owner` users receive non-empty results

  - [ ]* 8.9 Write property test: Emergency freeze suppression
    - **Property 13: Emergency freeze suppression**
    - **Validates: Requirements 6.3**
    - Generate groups with `EmergencyFreezeActive = true` and active recommendations
    - Assert queries return empty results and engine does not generate new recommendations

  - [ ]* 8.10 Write property test: Minimum eligible tasks precondition
    - **Property 14: Minimum eligible tasks precondition**
    - **Validates: Requirements 6.4**
    - Generate groups with fewer than 2 eligible tasks
    - Assert engine produces empty recommendation list regardless of shortfall

- [x] 9. Checkpoint — Commands and queries compilation
  - Ensure all command and query handlers compile without errors.
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. API layer — Recommendations controller
  - [x] 10.1 Create `RecommendationsController` with list and detail endpoints
    - Add `RecommendationsController.cs` in `Jobuler.Api/Controllers/`
    - `GET /spaces/{spaceId}/groups/{groupId}/recommendations` → dispatch `GetActiveRecommendationsQuery`
    - `GET /spaces/{spaceId}/runs/{runId}/recommendations` → dispatch `GetRecommendationsForRunQuery`
    - `GET /spaces/{spaceId}/tasks/{taskId}/recommendation` → dispatch `GetRecommendationForTaskQuery`
    - All endpoints require `[Authorize]`
    - Call `IPermissionService.RequirePermissionAsync` before dispatching queries
    - _Requirements: 3.2, 3.3, 6.1_

  - [x] 10.2 Add dismiss and accept action endpoints
    - `POST /spaces/{spaceId}/recommendations/{id}/dismiss` → dispatch `DismissRecommendationCommand`
    - `POST /spaces/{spaceId}/recommendations/{id}/accept` → dispatch `AcceptRecommendationCommand`
    - Accept body with optional `TriggerNewRun` boolean for accept endpoint
    - Add FluentValidation validators for request bodies
    - All endpoints require `[Authorize]` and permission checks
    - _Requirements: 4.1, 4.2, 4.3, 4.5_

  - [x] 10.3 Register `IRecommendationEngine` in DI container
    - Add service registration in `Program.cs` or the infrastructure DI extension
    - Register `RecommendationEngine` as scoped implementation of `IRecommendationEngine`
    - _Requirements: 1.1_

- [x] 11. Checkpoint — Full backend compilation
  - Ensure all backend code compiles without errors.
  - Ensure API endpoints are wired correctly.
  - Ensure all tests pass, ask the user if questions arise.

- [x] 12. Frontend — API hooks and types
  - [x] 12.1 Create TypeScript types for recommendation DTOs
    - Add recommendation types in `apps/web/lib/types/` or appropriate types file
    - Define `Recommendation`, `RecommendationBanner`, `RecommendationStatus` types
    - Match backend DTO shapes exactly
    - _Requirements: 3.2, 3.3_

  - [x] 12.2 Create React Query hooks for recommendation API
    - Add `useRecommendations(groupId)` hook — fetches active recommendations for a group
    - Add `useRecommendationsForRun(runId)` hook — fetches banner data for a solver run
    - Add `useRecommendationForTask(taskId)` hook — fetches inline suggestion for a task
    - Add `useDismissRecommendation()` mutation hook
    - Add `useAcceptRecommendation()` mutation hook with `triggerNewRun` option
    - Invalidate recommendation queries on dismiss/accept mutations
    - _Requirements: 3.1, 3.2, 3.3, 4.1, 4.3_

- [x] 13. Frontend — Recommendation banner component
  - [x] 13.1 Create `RecommendationBanner` component
    - Add `apps/web/components/recommendations/RecommendationBanner.tsx`
    - Display when recommendations exist for the current solver run
    - Show total uncovered slots count
    - Show up to 5 recommended task names with "+N more" indicator if more exist
    - Show affected date range (earliest start to latest end)
    - Include CTA button navigating to group task settings page
    - Use `useRecommendationsForRun` hook for data
    - _Requirements: 3.2_

  - [x] 13.2 Integrate `RecommendationBanner` into solver results / draft schedule views
    - Add banner to `DraftScheduleModal` or equivalent draft review component
    - Add banner to `ScheduleTab` or schedule results view
    - Conditionally render only when banner data is non-empty
    - _Requirements: 3.2_

- [x] 14. Frontend — Task inline suggestion component
  - [x] 14.1 Create `TaskDoubleShiftSuggestion` component
    - Add `apps/web/components/recommendations/TaskDoubleShiftSuggestion.tsx`
    - Display as inline chip/badge next to the `AllowsDoubleShift` toggle in group task settings
    - Show message: "Enabling could cover N additional slots (date range)"
    - Include "Enable" action button that calls `useAcceptRecommendation` with confirmation dialog
    - Include "Dismiss" action button that calls `useDismissRecommendation`
    - Use `useRecommendationForTask` hook for data
    - _Requirements: 3.3, 4.1, 4.3_

  - [x] 14.2 Integrate `TaskDoubleShiftSuggestion` into group task settings
    - In the existing group task settings component, render `TaskDoubleShiftSuggestion` next to each task's `AllowsDoubleShift` toggle
    - Only render when a recommendation exists for that task
    - Hide when `AllowsDoubleShift` is already true
    - _Requirements: 3.3, 6.2_

- [x] 15. Frontend — Accept flow with confirmation dialog
  - [x] 15.1 Implement accept recommendation flow with solver re-run prompt
    - On "Enable" click in either banner or inline suggestion: call accept mutation
    - On success: show confirmation dialog asking whether to trigger a new solver run
    - If user confirms: call accept with `triggerNewRun: true`, show success toast
    - If user declines: call accept with `triggerNewRun: false`, show success toast
    - If task already has double shift enabled: show informational message
    - Invalidate recommendation queries after accept
    - _Requirements: 4.1, 4.2, 4.5_

  - [x] 15.2 Implement dismiss recommendation flow
    - On "Dismiss" click: call dismiss mutation
    - On success: remove suggestion from UI (query invalidation handles this)
    - No confirmation dialog needed for dismiss
    - _Requirements: 4.3, 4.4_

- [x] 16. Checkpoint — Full frontend compilation
  - Ensure all frontend components compile without errors.
  - Ensure all tests pass, ask the user if questions arise.

- [ ]* 17. Unit tests
  - [ ]* 17.1 Write unit tests for `RecommendationEngine` core logic
    - Test engine produces empty list when no shortfall exists
    - Test engine produces empty list when all tasks already have double shift enabled
    - Test engine produces empty list when fewer than 2 eligible tasks
    - Test engine correctly identifies shortfall days
    - Test ranking with equal slot counts uses alphabetical tiebreaker
    - Test cap at 10 recommendations
    - _Requirements: 1.1, 1.2, 1.3, 2.2, 2.4, 2.5, 6.4_

  - [ ]* 17.2 Write unit tests for command handlers
    - Test dismiss changes status to `Dismissed` and sets `DismissedAt`/`DismissedByUserId`
    - Test accept enables double shift on task and marks as `Resolved`
    - Test accept with already-enabled task returns informational response
    - Test accept with deleted task marks as `Cleared` and returns 404
    - Test accept with `TriggerNewRun = true` enqueues solver run
    - Test permission denied returns 403
    - _Requirements: 4.1, 4.2, 4.3, 4.5, 6.1_

  - [ ]* 17.3 Write unit tests for query handlers
    - Test active filter returns only `Active` status recommendations
    - Test emergency freeze returns empty results
    - Test permission check returns empty for `View`-only users
    - Test banner DTO caps at 5 task names with remaining count
    - _Requirements: 4.4, 6.1, 6.3, 6.5_

  - [ ]* 17.4 Write unit tests for frontend components
    - Test `RecommendationBanner` renders correctly with mock data (3 tasks, 7 tasks with truncation)
    - Test `TaskDoubleShiftSuggestion` shows/hides based on recommendation presence
    - Test accept flow triggers API call and shows confirmation dialog
    - Test dismiss flow triggers API call and removes suggestion from UI
    - _Requirements: 3.2, 3.3, 4.1, 4.3_

- [x] 18. Final checkpoint — Ensure all tests pass
  - Run full test suite (backend + frontend).
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties defined in the design document (using FsCheck for C#)
- Unit tests validate specific examples and edge cases
- The recommendation engine is wrapped in try/catch at the integration point — failures never disrupt the core solver flow
- All recommendation data is tenant-scoped with RLS protection
- The engine runs after the solver completes, ensuring solver output and version are already persisted
- Frontend hooks use React Query for caching and automatic invalidation on mutations

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "3.1", "3.2"] },
    { "id": 2, "tasks": ["2.2", "5.1"] },
    { "id": 3, "tasks": ["5.2", "5.3"] },
    { "id": 4, "tasks": ["5.4", "5.5", "5.6", "5.7", "5.8", "5.9", "5.10"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 6, "tasks": ["8.1", "8.2", "8.3"] },
    { "id": 7, "tasks": ["8.4", "8.5", "8.6", "8.7", "8.8", "8.9", "8.10"] },
    { "id": 8, "tasks": ["10.1", "10.2", "10.3"] },
    { "id": 9, "tasks": ["12.1"] },
    { "id": 10, "tasks": ["12.2"] },
    { "id": 11, "tasks": ["13.1", "14.1"] },
    { "id": 12, "tasks": ["13.2", "14.2", "15.1", "15.2"] },
    { "id": 13, "tasks": ["17.1", "17.2", "17.3", "17.4"] }
  ]
}
```
