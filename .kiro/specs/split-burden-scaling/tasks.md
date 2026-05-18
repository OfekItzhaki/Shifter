# Implementation Plan: Split-Burden Scaling

## Overview

Implement post-solver burden level adjustment based on task split count. The feature adds a `SplitCount` column to `GroupTask`, introduces a pure `BurdenScalingService` for computing effective burden, modifies `AssignmentSnapshotService` to store effective burden in snapshots, updates fairness counters and exports to read from snapshots, and exposes effective burden in API responses and frontend displays. The solver continues to use the original burden level unchanged.

## Tasks

- [x] 1. Database migration and domain layer changes
  - [x] 1.1 Create database migration to add `split_count` column to `group_tasks`
    - Add `split_count INTEGER NOT NULL DEFAULT 1` with CHECK constraint `chk_split_count_positive CHECK (split_count >= 1)`
    - Existing rows automatically get value 1 via DEFAULT
    - _Requirements: 1.1, 1.2_

  - [x] 1.2 Create `BurdenScalingService` static class in `Jobuler.Domain.Tasks`
    - Implement `ComputeEffectiveBurden(TaskBurdenLevel originalBurden, int splitCount, int shiftDurationMinutes)` method
    - Return original burden when `splitCount <= 1`
    - Return original burden when `shiftDurationMinutes * splitCount < 240`
    - Otherwise return `(TaskBurdenLevel)Math.Max(0, (int)originalBurden - (splitCount - 1))`
    - _Requirements: 2.1, 2.2, 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 1.3 Update `GroupTask` domain entity with `SplitCount` property
    - Add `public int SplitCount { get; private set; } = 1;` property
    - Update `Create` factory method to accept `int splitCount = 1` parameter
    - Update `Update` method to accept `int splitCount = 1` parameter
    - Add validation: `SplitCount >= 1` in both Create and Update
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.4 Update EF Core configuration to map `SplitCount` column
    - Map `SplitCount` to `split_count` column in GroupTask entity configuration
    - _Requirements: 1.1_

  - [x]* 1.5 Write property test: Burden scaling formula correctness (FsCheck)
    - **Property 1: Burden scaling formula correctness**
    - Generate random `(TaskBurdenLevel, splitCount ∈ [1, 10], shiftDurationMinutes ∈ [1, 1440])` tuples
    - Verify: returns original burden when `splitCount == 1`
    - Verify: returns original burden when `shiftDurationMinutes * splitCount < 240`
    - Verify: returns `max(Easy, originalBurden - (splitCount - 1))` when threshold met
    - Verify: result is never below `Easy` (floor invariant)
    - **Validates: Requirements 2.1, 3.1, 3.5**

  - [x]* 1.6 Write unit tests for `BurdenScalingService`
    - Hard + split 2 (original duration ≥ 240) → Normal
    - Hard + split 3 (original duration ≥ 240) → Easy
    - Normal + split 2 (original duration ≥ 240) → Easy
    - Easy + split 5 (original duration ≥ 240) → Easy (floor)
    - Hard + split 2 but originalDuration = 120 min → Hard (threshold not met)
    - SplitCount = 1 → no change regardless of burden level
    - _Requirements: 2.1, 2.2, 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 2. Checkpoint - Domain layer verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify migration applies cleanly, `BurdenScalingService` computes correctly, and `GroupTask` persists `SplitCount`.

- [x] 3. API layer updates
  - [x] 3.1 Update `CreateGroupTaskCommand` and `UpdateGroupTaskCommand` to accept `SplitCount`
    - Add `int SplitCount = 1` to command records
    - Pass `SplitCount` to domain entity `Create`/`Update` methods in handlers
    - _Requirements: 1.1, 1.3_

  - [x] 3.2 Update request DTOs and FluentValidation for group task endpoints
    - Add `int SplitCount = 1` to `CreateGroupTaskRequest` and `UpdateGroupTaskRequest`
    - Add FluentValidation rule: `SplitCount >= 1`
    - _Requirements: 1.1, 1.3_

  - [x] 3.3 Update `GroupTaskResponseDto` to include effective burden and split count
    - Add `string EffectiveBurdenLevel` computed field
    - Add `int SplitCount` field
    - Call `BurdenScalingService.ComputeEffectiveBurden()` when mapping entity to DTO
    - _Requirements: 7.3_

  - [x]* 3.4 Write property test: Split count persistence round-trip (FsCheck)
    - **Property 2: Split count persistence round-trip**
    - Generate random `(splitCount ∈ [1, 10], shiftDurationMinutes ∈ [1, 1440])` pairs
    - Create or update a `GroupTask` with those values, read back entity
    - Verify `SplitCount` and `ShiftDurationMinutes` match input values
    - **Validates: Requirements 1.1, 1.3**

- [x] 4. AssignmentSnapshotService and fairness counter updates
  - [x] 4.1 Update `AssignmentSnapshotService` to use effective burden level
    - In `ResolveGroupTaskSlot`, replace `task.BurdenLevel.ToString().ToLower()` with `BurdenScalingService.ComputeEffectiveBurden(task.BurdenLevel, task.SplitCount, task.ShiftDurationMinutes).ToString().ToLower()`
    - DailySnapshots will now store the effective (split-adjusted) burden level
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 4.2 Update `UpdateFairnessCountersCommand` to use effective burden from snapshots
    - Modify hard task counting logic to read burden level from DailySnapshots instead of re-deriving from TaskTypes
    - Assignments with effective burden `Normal` or `Easy` (even if originally `Hard`) should NOT count toward `HardTasks7d`
    - _Requirements: 6.1, 6.2_

  - [x]* 4.3 Write property test: Solver payload preserves original burden (FsCheck)
    - **Property 3: Solver payload preserves original burden**
    - Generate GroupTasks with random `(BurdenLevel, splitCount, shiftDurationMinutes)` combinations
    - Build solver payload via `SolverPayloadNormalizer`
    - Verify `TaskSlotDto.burden_level` equals `task.BurdenLevel.ToString().ToLower()` (never the effective level)
    - **Validates: Requirements 4.1**

  - [x]* 4.4 Write unit tests for snapshot and fairness integration
    - Test: split task (Hard, split 2, 360 min original) → snapshot stores "normal"
    - Test: non-split task (Hard, split 1) → snapshot stores "hard"
    - Test: short task (Hard, split 2, 60 min original) → snapshot stores "hard" (threshold not met)
    - Test: fairness counter does NOT count split-reduced task as hard
    - _Requirements: 5.1, 5.2, 5.3, 6.1, 6.2_

- [x] 5. Checkpoint - Backend integration verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify end-to-end: create split task → solver uses original burden → publish → snapshot stores effective burden → fairness counter reflects effective burden.

- [x] 6. Export and frontend updates
  - [x] 6.1 Update `ExportSchedulePdfCommand` to use effective burden from DailySnapshots
    - Modify burden column logic to read from DailySnapshot `burden_level` field (already stores effective level after task 4.1)
    - _Requirements: 8.1_

  - [x] 6.2 Update frontend task list to display both original and effective burden
    - When `splitCount > 1`, show both original burden level and effective burden level in task configuration view
    - Use `EffectiveBurdenLevel` from `GroupTaskResponseDto`
    - _Requirements: 7.3_

  - [x] 6.3 Update frontend SubShiftEditor to send `splitCount` in API requests
    - Include `splitCount` field in create/update task request payloads
    - Compute `splitCount` from the number of sub-shifts selected in the editor
    - _Requirements: 1.1_

  - [x] 6.4 Verify schedule grid and statistics pages use snapshot burden level
    - Confirm schedule grid reads burden from DailySnapshot data (should already work after 4.1)
    - Confirm statistics/leaderboards compute from DailySnapshot records (should already work)
    - _Requirements: 7.1, 7.2_

- [x] 7. Final checkpoint - Full integration verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify: split task creation persists splitCount, solver payload uses original burden, snapshots store effective burden, fairness counters reflect effective burden, exports show effective burden, UI displays both levels for split tasks.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- FsCheck is used for C# property-based tests (xUnit + FsCheck)
- The solver (`SolverPayloadNormalizer`) requires NO changes — it already uses `task.BurdenLevel` directly
- `BurdenScalingService` is a static pure function — no DI registration needed
- DailySnapshots are the single source of truth for effective burden after publishing
- Fairness counters and exports read from snapshots, ensuring consistency without re-computation

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["1.4", "1.5", "1.6"] },
    { "id": 2, "tasks": ["3.1", "3.2"] },
    { "id": 3, "tasks": ["3.3", "3.4"] },
    { "id": 4, "tasks": ["4.1", "4.3"] },
    { "id": 5, "tasks": ["4.2", "4.4"] },
    { "id": 6, "tasks": ["6.1", "6.2", "6.3", "6.4"] }
  ]
}
```
