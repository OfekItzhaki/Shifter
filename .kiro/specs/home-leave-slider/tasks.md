# Implementation Plan: Home-Leave Slider

## Overview

Replace the abstract `priority_weight` concept with an intuitive 0–100 `balance_value` slider. Implementation flows from database schema change → domain entity → solver preview mode → API endpoints → frontend slider + preview hook + impact summary. A lightweight preview endpoint provides near-instant feedback by calling the solver synchronously with reduced time limits.

## Tasks

- [ ] 1. Database migration and domain entity update
  - [ ] 1.1 Create database migration to add `balance_value` column
    - Add `balance_value` integer column (NOT NULL, DEFAULT 50) to `home_leave_configs` table
    - Add CHECK constraint: `balance_value >= 0 AND balance_value <= 100`
    - Existing records receive default value 50 automatically
    - _Requirements: 9.1, 9.2, 9.3_
  - [ ] 1.2 Update `HomeLeaveConfig` domain entity with `BalanceValue` property
    - Add `public int BalanceValue { get; private set; } = 50;` to `HomeLeaveConfig`
    - Update `Create` factory method to accept optional `int balanceValue = 50` parameter
    - Update `Update` method to accept optional `int? balanceValue` parameter
    - Add private `ValidateBalanceValue(int value)` method that throws `InvalidOperationException` if value is outside [0, 100]
    - _Requirements: 9.4, 9.5, 1.3_
  - [ ] 1.3 Update EF Core configuration to map `BalanceValue`
    - Add property mapping in `HomeLeaveConfigConfiguration` for `balance_value` column
    - _Requirements: 9.1_

- [ ] 2. Backend: Update application layer commands and API
  - [ ] 2.1 Update `UpsertHomeLeaveConfigCommand` and handler to support `balance_value`
    - Add optional `int? BalanceValue` parameter to the command record
    - Pass `balanceValue` to `HomeLeaveConfig.Create` / `HomeLeaveConfig.Update` in the handler
    - If `BalanceValue` is null in the request, retain the currently stored value (backward compat)
    - _Requirements: 2.2, 2.3, 2.4, 10.3_
  - [ ] 2.2 Add FluentValidation rule for `BalanceValue`
    - When not null, validate `BalanceValue` is between 0 and 100 inclusive
    - Return 400 with message "balance_value must be between 0 and 100"
    - _Requirements: 2.3_
  - [ ] 2.3 Update `HomeLeaveConfigController` PUT endpoint to accept `balance_value`
    - Add `BalanceValue` to the request DTO
    - Pass value to the command
    - _Requirements: 2.2, 2.6_
  - [ ] 2.4 Update `HomeLeaveConfigController` GET endpoint to return `balance_value`
    - Include `balance_value` in the response DTO
    - _Requirements: 2.5_
  - [ ]* 2.5 Write property test: balance value validation (FsCheck)
    - **Property 1: Balance value validation**
    - Generate random integers and verify the domain accepts only values in [0, 100]
    - **Validates: Requirements 1.3, 2.3, 9.3, 9.5**
  - [ ]* 2.6 Write property test: persistence round-trip (FsCheck)
    - **Property 4: Balance value persistence round-trip**
    - Create a `HomeLeaveConfig` with a random valid `balance_value`, persist and read back, verify equality
    - **Validates: Requirements 2.4, 2.5**
  - [ ]* 2.7 Write property test: backward compatibility — omit retains stored value (FsCheck)
    - **Property 9: Backward compatibility — omitting balance_value preserves stored value**
    - For any existing config with stored `balance_value` B, update without providing `balance_value`, verify stored value remains B
    - **Validates: Requirements 10.3**

- [ ] 3. Checkpoint - Database and API verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify migration applies cleanly and GET/PUT endpoints include `balance_value`.

- [ ] 4. Solver: Add `preview_mode` and `balance_value` weight mapping
  - [ ] 4.1 Update solver Pydantic models for `balance_value` and `preview_mode`
    - Add `balance_value: int = 50` to `HomeLeaveConfig` model
    - Add `preview_mode: bool = False` to `SolverInput` model
    - Add `solver_time_ms: int = 0` to `SolverOutput` model
    - _Requirements: 3.1, 8.1, 5.6_
  - [ ] 4.2 Implement `balance_value × 4` weight mapping in home-leave preference
    - Replace hardcoded weight (200) with `balance_value * 4` in `add_home_leave_eligibility_preference`
    - When `balance_value` is absent/None, default to 50 (weight = 200) for backward compatibility
    - _Requirements: 3.2, 3.3, 3.4, 3.6_
  - [ ] 4.3 Implement `preview_mode` solver behavior
    - When `preview_mode` is `true`: set CP-SAT time limit to 3 seconds, `num_workers` to 1, disable solution logging
    - When `preview_mode` is `false` or absent: no behavioral change from current implementation
    - Record wall-clock solve time in `solver_time_ms` output field
    - _Requirements: 8.2, 8.3, 8.4, 8.5, 8.6, 8.7_
  - [ ]* 4.4 Write property test: linear weight mapping (Hypothesis)
    - **Property 2: Linear weight mapping**
    - For any `balance_value` in [0, 100], verify weight equals `balance_value × 4`
    - **Validates: Requirements 3.2, 3.3, 3.4**
  - [ ]* 4.5 Write property test: hard constraints invariant (Hypothesis)
    - **Property 3: Hard constraints invariant under balance_value changes**
    - For any two distinct `balance_value` settings, verify the set of hard constraints is identical
    - **Validates: Requirements 3.5**
  - [ ]* 4.6 Write property test: solver status mapping (Hypothesis)
    - **Property 8: Solver status mapping**
    - Verify solver termination maps correctly to "optimal", "feasible", or "no_solution"
    - **Validates: Requirements 5.5**

- [ ] 5. Checkpoint - Solver verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify solver respects `preview_mode` and `balance_value` weight mapping.

- [ ] 6. Backend: Preview endpoint and solver payload integration
  - [ ] 6.1 Create `PreviewHomeLeaveCommand` and `HomeLeavePreviewResult` DTO
    - Define command record with `SpaceId`, `GroupId`, `BalanceValue` parameters
    - Define `HomeLeavePreviewResponse` record with `Status`, `PeopleHomeCount`, `PeopleAtBaseCount`, `TotalHomeLeaveSlots`, `CoverageGaps`, `FairnessSpread`, `SolverTimeMs`
    - Define `CoverageGapDto` record with `StartsAt`, `EndsAt`, `AvailableCount`
    - _Requirements: 5.1, 5.2, 5.5, 5.6_
  - [ ] 6.2 Implement `PreviewHomeLeaveHandler`
    - Validate group exists, is closed-base, and has home-leave config
    - Build solver payload with overridden `balance_value` and `preview_mode = true`
    - Call solver synchronously via `ISolverClient.SolveAsync` with 5-second HTTP timeout
    - Transform `SolverOutput` into `HomeLeavePreviewResponse` (counts, gaps, fairness)
    - On timeout or network error, return `status: "no_solution"` with `solver_time_ms: 0`
    - _Requirements: 4.2, 4.3, 4.4, 4.5, 4.6, 4.8, 4.9_
  - [ ] 6.3 Add `BuildPreviewAsync` method to `SolverPayloadNormalizer`
    - Build payload identical to normal run but override `balance_value` with provided value
    - Set `preview_mode = true` in the payload
    - _Requirements: 4.2_
  - [ ] 6.4 Update `SolverPayloadNormalizer.BuildAsync` to include `balance_value`
    - Include stored `balance_value` from `HomeLeaveConfig` in the `HomeLeaveConfigDto`
    - _Requirements: 3.1_
  - [ ] 6.5 Create preview controller endpoint
    - Add `POST /spaces/{spaceId}/groups/{groupId}/home-leave-preview` to `HomeLeaveConfigController`
    - Require `constraints.manage` permission
    - Accept `HomeLeavePreviewRequest` body with `BalanceValue`
    - Validate `BalanceValue` is in [0, 100]
    - Dispatch `PreviewHomeLeaveCommand` via MediatR
    - _Requirements: 4.1, 4.7, 4.9_
  - [ ]* 6.6 Write property test: solver payload includes correct balance_value (FsCheck)
    - **Property 5: Solver payload includes correct balance_value**
    - For any stored `balance_value` B, verify payload contains `balance_value == B`; for preview override P, verify payload contains `balance_value == P`
    - **Validates: Requirements 3.1, 4.2**
  - [ ]* 6.7 Write property test: preview result transformation completeness (FsCheck)
    - **Property 6: Preview result transformation completeness**
    - For any valid `SolverOutput`, verify `people_home_count`, `people_at_base_count`, and `total_home_leave_slots` are computed correctly
    - **Validates: Requirements 5.1**
  - [ ]* 6.8 Write property test: coverage gap calculation (FsCheck)
    - **Property 7: Coverage gap calculation correctness**
    - For any set of assignments and group config, verify gaps are reported when available people < N - C
    - **Validates: Requirements 5.2**

- [ ] 7. Checkpoint - Preview endpoint verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify preview endpoint returns correct impact data for different `balance_value` inputs.

- [ ] 8. Frontend: Slider component and preview integration
  - [ ] 8.1 Create `BalanceSlider` component
    - Horizontal range input accepting values 0–100
    - Left label: "יותר אנשים בבסיס", right label: "יותר אנשים בבית"
    - Display current numeric value adjacent to the slider track
    - Keyboard accessible: arrow keys ±1, Page Up/Down ±10
    - Accept `value` and `onChange` props; default to 50 when no stored value
    - _Requirements: 1.1, 1.2, 1.3, 1.5, 1.6, 1.7_
  - [ ] 8.2 Create `useHomeLeavePreview` custom hook
    - Accept `spaceId`, `groupId`, `balanceValue` parameters
    - Debounce requests with 500ms interval
    - Cancel pending requests when a new value arrives
    - Ignore stale responses when a newer request is in flight
    - Return `{ data, isLoading, error }` state
    - Call `POST /spaces/{spaceId}/groups/{groupId}/home-leave-preview`
    - _Requirements: 6.1, 6.2, 6.3, 6.7_
  - [ ] 8.3 Create `ImpactSummary` component
    - Display metrics in Hebrew: "אנשים בבית", "אנשים בבסיס", "סה״כ חופשות", "פער הוגנות"
    - Horizontal bar visualization showing people home vs. at base ratio
    - Show "כיסוי מלא" when zero coverage gaps
    - Show warning with gap hours and minimum available count when gaps exist
    - Highlight fairness metric with warning color when `fairness_spread > 0.15`
    - Show "לא נמצא פתרון עם ההגדרות הנוכחיות" when status is "no_solution"
    - Show "תוצאה משוערת" indicator when status is "feasible"
    - Display loading indicator while preview is in flight
    - Show error message "לא ניתן לטעון תצוגה מקדימה" on failure, retain last result
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 5.3, 5.4, 6.4, 6.5_
  - [ ] 8.4 Integrate slider and impact summary into home-leave configuration panel
    - Add `BalanceSlider` to the home-leave config section (replacing `priority_weight` input)
    - Place `ImpactSummary` directly below the slider
    - Wire slider `onChange` to `useHomeLeavePreview` hook
    - On panel open with existing `balance_value`, trigger initial preview request
    - On save, include `balance_value` in the PUT request body
    - Retain existing config fields (`min_rest_hours`, `eligibility_threshold_hours`, `leave_capacity`, `leave_duration_hours`) alongside the slider
    - _Requirements: 1.4, 6.1, 6.6, 10.4_
  - [ ] 8.5 Update API client types and functions
    - Add `balance_value` to `HomeLeaveConfigDto` interface
    - Add `createHomeLeavePreview` API function
    - Update `updateHomeLeaveConfig` payload type to include optional `balance_value`
    - _Requirements: 2.2, 2.5, 4.1_

- [ ] 9. Final checkpoint - Full integration verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify slider renders, preview updates on slider change, and save persists the value.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The preview endpoint is an intentional exception to the "never call solver synchronously" architecture rule — justified because preview results are ephemeral and time-bounded (5s max)
- Weight mapping formula: `weight = balance_value × 4` (0→0, 50→200, 100→400)
- FsCheck is used for C# property tests, Hypothesis for Python solver property tests

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2", "1.3"] },
    { "id": 2, "tasks": ["2.1", "2.2", "4.1"] },
    { "id": 3, "tasks": ["2.3", "2.4", "2.5", "2.6", "2.7", "4.2", "4.3"] },
    { "id": 4, "tasks": ["4.4", "4.5", "4.6", "6.1"] },
    { "id": 5, "tasks": ["6.2", "6.3", "6.4"] },
    { "id": 6, "tasks": ["6.5", "6.6", "6.7", "6.8"] },
    { "id": 7, "tasks": ["8.1", "8.2", "8.5"] },
    { "id": 8, "tasks": ["8.3", "8.4"] }
  ]
}
```
