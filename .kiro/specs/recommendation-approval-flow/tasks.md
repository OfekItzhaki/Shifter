# Implementation Plan: Recommendation Approval Flow

## Overview

This plan transforms the recommendation system from an auto-action model into a passive informational model. The implementation proceeds in three tracks: (1) backend simplification by removing the accept endpoint and command, (2) a new informational RecommendationCard in the HomeLeaveConfigPanel, and (3) task info badges in the schedule grid. Each track is wired together at the end with localization and integration tests.

## Tasks

- [x] 1. Backend simplification — remove accept logic and extend schedule response
  - [x] 1.1 Delete `AcceptRecommendationCommand.cs` and remove the accept endpoint from `RecommendationsController`
    - Delete `apps/api/Jobuler.Application/Scheduling/Commands/AcceptRecommendationCommand.cs`
    - Remove the `Accept` action method and `AcceptRecommendationRequest` record from `apps/api/Jobuler.Api/Controllers/RecommendationsController.cs`
    - Remove any validator registrations referencing `AcceptRecommendationCommand`
    - _Requirements: 1.1, 1.2, 4.1_

  - [x] 1.2 Remove `EnableDoubleShift` method from `GroupTask` domain entity
    - Locate the `EnableDoubleShift(Guid updatedByUserId)` method in the `GroupTask` entity under `Jobuler.Domain`
    - Delete the method — the full `Update(...)` method remains the only way to change `AllowsDoubleShift`
    - _Requirements: 1.1, 4.4_

  - [x] 1.3 Create `TaskConfigSummaryDto` and extend `GetGroupScheduleQuery` response
    - Add `TaskConfigSummaryDto` record to `Jobuler.Application/Scheduling/Models/` (or alongside the query)
    - Modify `GetGroupScheduleQuery` handler to join `GroupTask` data for all tasks in the published schedule
    - Return a new `GroupScheduleResponseDto` containing both `Assignments` and `TaskConfigurations` dictionary keyed by task ID
    - _Requirements: 7.1, 7.2_

  - [x]* 1.4 Write unit tests for backend changes
    - Test that the dismiss handler sets status to `Dismissed` without modifying `GroupTask.AllowsDoubleShift`
    - Test that the schedule query response includes `TaskConfigurations` for each task
    - Test that calling the removed accept endpoint returns 404
    - _Requirements: 1.3, 4.1, 4.2, 7.1_

- [x] 2. Checkpoint — Ensure backend compiles and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Frontend API client cleanup and type updates
  - [x] 3.1 Remove `acceptRecommendation` function and related types from `lib/api/recommendations.ts`
    - Delete `acceptRecommendation` function
    - Delete `AcceptRecommendationResult` interface and `AcceptRecommendationOutcome` type
    - _Requirements: 4.1_

  - [x] 3.2 Remove `useAcceptRecommendation` hook from `lib/query/hooks/useRecommendations.ts`
    - Delete the `useAcceptRecommendation` function and its `AcceptRecommendationParams` interface
    - Remove the `AcceptRecommendationResult` import
    - Keep `useRecommendations`, `useRecommendationsForRun`, `useRecommendationForTask`, and `useDismissRecommendation` unchanged
    - _Requirements: 4.1_

  - [x] 3.3 Update schedule API client to handle new response shape
    - Add `TaskConfigSummaryDto` TypeScript interface to `lib/api/schedule.ts`
    - Add `GroupScheduleResponseDto` interface wrapping `assignments` and `taskConfigurations`
    - Update any schedule-fetching functions that consume the group schedule endpoint to destructure the new response shape
    - _Requirements: 7.1, 7.2_

- [x] 4. Informational RecommendationCard component
  - [x] 4.1 Create `RecommendationCard` component at `components/recommendations/RecommendationCard.tsx`
    - Fetch active recommendations via `useRecommendations(spaceId, groupId)`
    - Display task names and total uncovered slot count
    - Render a "Go to Tasks" button that navigates to `?tab=tasks`
    - Render a "Dismiss" button that calls `useDismissRecommendation`
    - Render nothing when no active recommendations exist or while loading
    - Use localized strings via `useTranslations("recommendations")`
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 3.1_

  - [x] 4.2 Integrate `RecommendationCard` into `HomeLeaveConfigPanel`
    - Import `RecommendationCard` in `components/home-leave/HomeLeaveConfigPanel.tsx`
    - Render it above the `EmergencyFreezeBanner` component
    - Pass `spaceId` and `groupId` props
    - _Requirements: 2.1_

  - [x]* 4.3 Write property test for RecommendationCard rendering
    - **Property 2: Recommendation card displays all task names and slot counts**
    - Use `fast-check` to generate arbitrary sets of active recommendations with varying task names and slot counts
    - Assert that the rendered output contains every task name and the total uncovered slot count
    - **Validates: Requirements 2.2**

  - [x]* 4.4 Write unit tests for RecommendationCard
    - Test card renders when recommendations exist
    - Test card does not render when recommendations array is empty
    - Test "Go to Tasks" button navigates to `?tab=tasks`
    - Test "Dismiss" button calls dismiss mutation
    - _Requirements: 2.1, 2.4, 2.5, 3.1_

- [x] 5. Checkpoint — Ensure recommendation card works end-to-end
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Task info badge and popover in schedule grid
  - [x] 6.1 Create `TaskInfoPopover` component at `components/schedule/TaskInfoPopover.tsx`
    - Accept `TaskConfigSummaryDto` as props
    - Display all task configuration fields with localized labels
    - Show "24/7" when `dailyStartTime` and `dailyEndTime` are null
    - Show split count only when > 1
    - Show a "default settings" message when all values are defaults (allowsDoubleShift=false, allowsOverlap=false, no time window, burdenLevel="Normal", no qualifications, splitCount=1)
    - Close on click-outside or blur
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 6.2 Create `TaskInfoBadge` component at `components/schedule/TaskInfoBadge.tsx`
    - Accept `TaskConfigSummaryDto` as props
    - Render a small "ℹ" icon button with `aria-label="Task configuration info"`
    - Render nothing if config data is unavailable (null/undefined)
    - Open `TaskInfoPopover` on click
    - Style as visually subtle (small, muted color)
    - _Requirements: 5.1, 5.2, 5.3, 7.3_

  - [x] 6.3 Integrate `TaskInfoBadge` into `ScheduleTable2D` column headers
    - Modify `components/schedule/ScheduleTable2D.tsx` to accept an optional `taskConfigurations` prop (`Record<string, TaskConfigSummaryDto>`)
    - Render `TaskInfoBadge` next to each task name in the `<th>` column header
    - Pass the relevant config from the map to each badge
    - _Requirements: 5.1, 7.1_

  - [x]* 6.4 Write property test for TaskInfoBadge presence and accessibility
    - **Property 3: Task info badge presence and accessibility**
    - Use `fast-check` to generate arbitrary sets of tasks with configuration data
    - Assert each task column header contains exactly one badge with correct `aria-label`
    - **Validates: Requirements 5.1, 5.3**

  - [x]* 6.5 Write property test for TaskInfoPopover configuration display
    - **Property 4: Task info popover displays correct configuration**
    - Use `fast-check` to generate arbitrary `TaskConfigSummaryDto` values
    - Assert the popover displays correct values for each non-default field and shows split count only when > 1
    - **Validates: Requirements 6.1**

  - [x]* 6.6 Write unit tests for TaskInfoBadge and TaskInfoPopover
    - Test badge hidden when config is null
    - Test popover shows "default settings" message for default config
    - Test popover closes on click-outside
    - Test localized strings render correctly
    - _Requirements: 5.3, 6.2, 6.3, 6.4, 7.3_

- [x] 7. Localization — add translation keys
  - [x] 7.1 Add recommendation card and task info localization keys to `messages/en.json`, `messages/he.json`, and `messages/ru.json`
    - Add `recommendations.cardTitle`, `recommendations.cardDescription`, `recommendations.goToTasks`, `recommendations.dismiss`, `recommendations.slotsCount`
    - Add `schedule.taskInfoLabel`, `schedule.taskInfo.doubleShift`, `schedule.taskInfo.overlap`, `schedule.taskInfo.timeWindow`, `schedule.taskInfo.allDay`, `schedule.taskInfo.burden`, `schedule.taskInfo.qualifications`, `schedule.taskInfo.splitCount`, `schedule.taskInfo.defaultSettings`
    - _Requirements: 2.2, 2.3, 6.2_

- [x] 8. Integration wiring and cleanup
  - [x] 8.1 Remove or deprecate `TaskDoubleShiftSuggestion` component
    - The old action-oriented `TaskDoubleShiftSuggestion` component at `components/recommendations/TaskDoubleShiftSuggestion.tsx` is no longer needed
    - Remove imports and usages across the codebase
    - _Requirements: 1.1, 4.1_

  - [x] 8.2 Wire schedule data fetching to pass `taskConfigurations` to `ScheduleTable2D`
    - In the page/container that renders `ScheduleTable2D`, destructure the new `GroupScheduleResponseDto` response
    - Pass `taskConfigurations` to `ScheduleTable2D` as a prop
    - _Requirements: 7.1, 7.2_

  - [x]* 8.3 Write property test for dismiss preserving task state
    - **Property 1: Dismiss preserves task state**
    - Use `fast-check` to generate arbitrary recommendation and GroupTask state combinations
    - Assert that after dismiss, recommendation status is `Dismissed` and `AllowsDoubleShift` is unchanged
    - **Validates: Requirements 1.1, 1.3, 4.2**

  - [x]* 8.4 Write integration tests
    - Test that the accept endpoint returns 404 after removal
    - Test that the schedule endpoint includes `taskConfigurations` in response
    - Test that the recommendation engine still generates recommendations after refactor
    - Test that the task update endpoint remains the only way to enable double shift
    - _Requirements: 4.1, 4.3, 4.4, 7.1, 7.2_

- [x] 9. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The backend uses C# (.NET) with MediatR and FluentValidation
- The frontend uses TypeScript/Next.js with TanStack Query and next-intl for localization
- The schedule response shape change is a breaking change — frontend must be updated in the same release

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "3.1", "3.2"] },
    { "id": 2, "tasks": ["1.4", "3.3", "7.1"] },
    { "id": 3, "tasks": ["4.1", "6.1"] },
    { "id": 4, "tasks": ["4.2", "4.3", "4.4", "6.2"] },
    { "id": 5, "tasks": ["6.3", "6.4", "6.5", "6.6"] },
    { "id": 6, "tasks": ["8.1", "8.2"] },
    { "id": 7, "tasks": ["8.3", "8.4"] }
  ]
}
```
