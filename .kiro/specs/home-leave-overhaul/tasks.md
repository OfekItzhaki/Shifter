# Implementation Plan: Home-Leave Overhaul

## Overview

Replace the abstract threshold/balance home-leave configuration with a three-mode system: Automatic (smart slider centered on an optimal ratio), Manual Override (explicit day inputs with real-time feasibility feedback), and Emergency Freeze (immediate leave suspension). Implementation flows from database migration → domain entity updates → application services (OptimalRatioCalculator, FeasibilityEngine) → API endpoint updates → SolverPayloadNormalizer changes → frontend components (ModeSelector, RatioSlider, ManualModeSection, EmergencyFreezeBanner, FeasibilityIndicator) → data migration → RTL/LTR handling.

## Tasks

- [x] 1. Database migration and domain entity updates
  - [x] 1.1 Create database migration 053 to add new columns
    - Add `mode` TEXT NOT NULL DEFAULT 'automatic' to `home_leave_configs`
    - Add `base_days` INTEGER NOT NULL DEFAULT 7
    - Add `home_days` INTEGER NOT NULL DEFAULT 2
    - Add `emergency_freeze_active` BOOLEAN NOT NULL DEFAULT FALSE
    - Add `emergency_use_for_scheduling` BOOLEAN NOT NULL DEFAULT FALSE
    - Add `freeze_started_at` TIMESTAMPTZ nullable
    - Add `pre_freeze_mode` TEXT NOT NULL DEFAULT 'automatic'
    - Add CHECK constraints: `chk_mode_valid`, `chk_pre_freeze_mode_valid`, `chk_base_days_min`, `chk_home_days_min`
    - _Requirements: 10.2_

  - [x] 1.2 Add data migration logic to compute `base_days` and `home_days` from existing data
    - UPDATE existing rows: `base_days = GREATEST(1, ROUND(eligibility_threshold_hours / 24))`
    - UPDATE existing rows: `home_days = GREATEST(1, ROUND(leave_duration_hours / 24))`
    - SET `min_rest_hours = 0` for all existing rows
    - _Requirements: 10.1, 10.3, 10.4_

  - [x] 1.3 Add `HomeLeaveMode` enum to Domain layer
    - Create `HomeLeaveMode` enum with `Automatic` and `Manual` values in `Jobuler.Domain.Groups`
    - _Requirements: 1.1_

  - [x] 1.4 Update `HomeLeaveConfig` domain entity with new fields and methods
    - Add properties: `Mode`, `BaseDays`, `HomeDays`, `EmergencyFreezeActive`, `EmergencyUseForScheduling`, `FreezeStartedAt`, `PreFreezeMode`
    - Add `SetMode(HomeLeaveMode mode)` method — switches mode, recalculates solver params
    - Add `SetRatio(int baseDays, int homeDays)` method — validates and converts to solver params
    - Add `SetSliderPosition(int sliderValue, int optimalBaseDays, int optimalHomeDays)` method
    - Add `ActivateEmergencyFreeze(bool useForScheduling)` — records pre-freeze mode, sets freeze state
    - Add `DeactivateEmergencyFreeze()` — restores pre-freeze mode, clears freeze state
    - Add validation: baseDays >= 1, homeDays >= 1, leaveDurationHours in [12, 168]
    - Update `Create` and `Update` factory/methods to support new fields
    - _Requirements: 1.1, 1.2, 1.4, 4.6, 6.1, 6.7, 7.1, 7.5_

  - [x] 1.5 Update EF Core configuration to map new columns
    - Map `Mode` as string column `mode`
    - Map `BaseDays`, `HomeDays`, `EmergencyFreezeActive`, `EmergencyUseForScheduling`, `FreezeStartedAt`, `PreFreezeMode`
    - _Requirements: 10.2_

- [x] 2. Application layer services
  - [x] 2.1 Implement `OptimalRatioCalculator` service
    - Create `IOptimalRatioCalculator` interface in Application layer
    - Create `OptimalRatioResult` record with `BaseDays`, `HomeDays`, `IsReduced`
    - Implement iterative formula: `home_days = ceil(leave_duration_hours / 24)`, converge `base_days = ceil((coverage × cycle_length) / (members - capacity))`
    - Register in DI container
    - _Requirements: 2.1, 2.2, 2.4, 2.5, 2.6, 12.1, 12.3_

  - [x] 2.2 Implement `FeasibilityEngine` service
    - Create `IFeasibilityEngine` interface in Application layer
    - Create `FeasibilityResult` record with `IsFeasible`, `MaxFeasibleHomeDays`, `Reason`
    - Implement feasibility check: `(memberCount - leaveCapacity) >= coverageRequirement`
    - Return localized reason strings when not feasible
    - Register in DI container
    - _Requirements: 4.2, 4.3, 4.4, 4.7, 5.1, 5.2, 5.3, 5.4, 12.2_

  - [ ]* 2.3 Write property test: Optimal Ratio Formula Correctness (FsCheck)
    - **Property 2: Optimal Ratio Formula Correctness**
    - Generate random valid inputs (memberCount ≥ 2, leaveCapacity ≥ 1, leaveDurationHours ∈ [12, 168], coverageRequirement ≥ 1) where memberCount > leaveCapacity + coverageRequirement
    - Verify output baseDays and homeDays are positive integers
    - Verify (memberCount - leaveCapacity) >= coverageRequirement is satisfied
    - **Validates: Requirements 2.4, 2.2**

  - [ ]* 2.4 Write property test: Feasibility Engine Correctness (FsCheck)
    - **Property 5: Feasibility Engine Correctness**
    - Generate random configurations (memberCount, leaveCapacity, baseDays, homeDays, coverageRequirement)
    - Verify IsFeasible = true iff (memberCount - leaveCapacity) >= coverageRequirement
    - When feasible, verify MaxFeasibleHomeDays >= homeDays
    - When not feasible, verify Reason is non-empty
    - **Validates: Requirements 4.2, 4.3, 4.4, 5.2**

- [x] 3. Checkpoint - Domain and services verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify migration applies cleanly, domain entity validates correctly, and services compute expected results.

- [x] 4. API endpoint updates
  - [x] 4.1 Update `UpsertHomeLeaveConfigCommand` to support new mode system
    - Add `Mode`, `BaseDays`, `HomeDays`, `SliderValue`, `EmergencyFreezeActive`, `EmergencyUseForScheduling` to command record
    - Update handler to call appropriate domain methods based on mode
    - Call `OptimalRatioCalculator` when in Automatic mode to compute optimal ratio
    - Call `FeasibilityEngine` to include feasibility in response
    - _Requirements: 1.2, 1.4, 3.6, 4.5, 8.1, 8.2_

  - [x] 4.2 Update `HomeLeaveConfigController` PUT endpoint with new request DTO
    - Replace `UpsertHomeLeaveConfigRequest` with new record including `Mode`, `BaseDays`, `HomeDays`, `SliderValue`, `LeaveDurationHours`, `LeaveCapacity`, `EmergencyFreezeActive`, `EmergencyUseForScheduling`
    - Add FluentValidation: Mode required, BaseDays/HomeDays required for Manual, SliderValue required for Automatic
    - Permission check via `IPermissionService`
    - _Requirements: 1.2, 4.1, 4.5, 4.6_

  - [x] 4.3 Add `GET optimal-ratio` endpoint to controller
    - Add `GET /spaces/{spaceId}/groups/{groupId}/home-leave-config/optimal-ratio`
    - Query group member count and coverage requirement
    - Call `OptimalRatioCalculator` and return `OptimalRatioResponse`
    - _Requirements: 2.1, 2.3, 2.6_

  - [x] 4.4 Update `HomeLeaveConfigController` GET endpoint response DTO
    - Return `HomeLeaveConfigResponse` with all new fields: `Mode`, `BaseDays`, `HomeDays`, `EmergencyFreezeActive`, `EmergencyUseForScheduling`, `FreezeStartedAt`, `OptimalBaseDays`, `OptimalHomeDays`, `OptimalIsReduced`
    - Compute optimal ratio on read and include in response
    - _Requirements: 1.3, 2.2, 2.3_

  - [x] 4.5 Update preview endpoint to accept mode and ratio parameters
    - Extend `HomeLeavePreviewRequest` with `Mode`, `BaseDays`, `HomeDays`, `SliderValue`, `LeaveDurationHours`
    - Call `FeasibilityEngine` and return feasibility result alongside solver preview
    - _Requirements: 5.1, 5.5, 5.6_

  - [ ]* 4.6 Write property test: Day Input Validation (FsCheck)
    - **Property 6: Day Input Validation**
    - Generate random integer values for baseDays and homeDays
    - Verify values < 1 are rejected with validation error
    - Verify values in [1, 14] (base) and [1, 7] (home) are accepted
    - **Validates: Requirements 4.6**

  - [ ]* 4.7 Write property test: Leave Duration Validation (FsCheck)
    - **Property 10: Leave Duration Validation**
    - Generate random decimal values for leaveDurationHours
    - Verify values outside [12, 168] are rejected
    - Verify values within [12, 168] are accepted and preserved unchanged
    - **Validates: Requirements 9.2**

- [x] 5. Checkpoint - API verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify all endpoints return correct data for Automatic, Manual, and Emergency Freeze modes.

- [x] 6. SolverPayloadNormalizer updates
  - [x] 6.1 Update `SolverPayloadNormalizer` to handle mode-based payload construction
    - Automatic mode: `eligibility_threshold_hours = base_days × 24`, `balance_value` from slider mapping, `min_rest_hours = 0`
    - Manual mode: `eligibility_threshold_hours = base_days × 24`, `balance_value = 50` (neutral), `min_rest_hours = 0`
    - Emergency freeze + use for scheduling: `balance_value = 0`, `eligibility_threshold_hours = 9999`, `min_rest_hours = 0`
    - Emergency freeze + don't use for scheduling: omit `home_leave_config` entirely
    - _Requirements: 8.1, 8.2, 8.4, 8.5, 8.6_

  - [x] 6.2 Update `BuildPreviewAsync` to support new mode parameters
    - Accept mode, baseDays, homeDays, sliderValue in preview build
    - Construct payload according to mode rules
    - _Requirements: 5.5, 8.1_

  - [ ]* 6.3 Write property test: Ratio-to-Solver-Params Round Trip (FsCheck)
    - **Property 4: Ratio-to-Solver-Params Round Trip**
    - Generate random valid (baseDays ∈ [1, 14], homeDays ∈ [1, 7]) pairs
    - Verify `eligibility_threshold_hours = baseDays × 24`
    - Verify `balance_value ∈ [0, 100]`
    - Verify `floor(eligibility_threshold_hours / 24)` equals original baseDays
    - **Validates: Requirements 3.6, 4.5, 8.1, 8.2**

  - [ ]* 6.4 Write property test: Emergency Freeze Solver Payload (FsCheck)
    - **Property 7: Emergency Freeze Solver Payload**
    - Generate random HomeLeaveConfig with EmergencyFreezeActive = true
    - If EmergencyUseForScheduling = true, verify payload contains balance_value = 0
    - If EmergencyUseForScheduling = false, verify payload omits home_leave_config entirely
    - **Validates: Requirements 6.1, 6.5, 6.6, 8.5, 8.6**

  - [ ]* 6.5 Write property test: Min Rest Hours Invariant (FsCheck)
    - **Property 9: Min Rest Hours Invariant**
    - Generate random HomeLeaveConfig with mode Automatic or Manual
    - Verify solver payload always has min_rest_hours = 0
    - **Validates: Requirements 8.4, 3.8**

- [x] 7. Checkpoint - Solver payload verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify solver payload is correctly constructed for each mode and emergency freeze state.

- [x] 8. Frontend: Core components
  - [x] 8.1 Create `ModeSelector` component
    - Segmented control with Automatic and Manual options
    - Accept `value` and `onChange` props typed as `HomeLeaveMode`
    - Localized labels using next-intl (Hebrew, English, Russian)
    - Accessible with keyboard navigation and ARIA attributes
    - _Requirements: 1.1, 1.5, 11.1_

  - [x] 8.2 Create `RatioSlider` component (replaces `BalanceSlider`)
    - Accept `optimalBaseDays`, `optimalHomeDays`, `value` (0-100), `onChange`, `locale` props
    - Display current ratio as localized text (e.g., "5:2 ימים בסיס/בית")
    - Gradient background from conservative to generous
    - Center position represents optimal ratio
    - Step of 5, keyboard accessible (arrow keys ±5, Page Up/Down ±10)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.7_

  - [x] 8.3 Create `ManualModeSection` component
    - Two numeric input fields: days at base, days at home
    - Validation: minimum 1 for both fields
    - Display feasibility feedback inline (green/red indicator)
    - Debounce feasibility API calls by 500ms
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.6, 4.7_

  - [x] 8.4 Create `EmergencyFreezeBanner` component
    - Prominent toggle for activating/deactivating emergency freeze
    - When active: display red banner with freeze duration timer
    - Include "Use for scheduling?" toggle option
    - Confirmation dialog before activation
    - _Requirements: 6.1, 6.3, 6.4, 6.7_

  - [x] 8.5 Create `FeasibilityIndicator` component
    - Green indicator when feasible with effective ratio display
    - Red indicator when not feasible with localized explanation
    - Accept `feasibilityResult` prop
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 8.6 Create `LeaveDurationInput` component
    - Numeric input for leave duration displayed in days
    - Internally stores as hours (value × 24)
    - Validation: 0.5 to 7 days (12 to 168 hours)
    - Shared between Automatic and Manual modes
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [ ] 9. Frontend: RTL/LTR slider handling and integration
  - [x] 9.1 Implement RTL/LTR direction handling in `RatioSlider`
    - Use `dir="rtl"` on slider container when locale is Hebrew
    - Flip gradient direction: `to right` for RTL, `to left` for LTR
    - Swap label positions based on direction
    - Value semantics remain consistent (0 = conservative, 100 = generous) regardless of direction
    - Use `useDirection()` hook or derive from locale
    - _Requirements: 11.2, 11.3, 11.4_

  - [x] 9.2 Integrate all components into `HomeLeaveConfigPanel`
    - Wire `ModeSelector` to show/hide Automatic vs Manual sections
    - Wire `RatioSlider` onChange to feasibility preview API
    - Wire `ManualModeSection` inputs to feasibility API
    - Wire `EmergencyFreezeBanner` to emergency freeze API calls
    - Display `FeasibilityIndicator` below active mode section
    - Include `LeaveDurationInput` shared between modes
    - On save, call updated PUT endpoint with all mode-specific fields
    - _Requirements: 1.5, 1.6, 5.6, 9.3_

  - [x] 9.3 Update API client types and functions
    - Update `HomeLeaveConfigDto` interface with new fields
    - Add `getOptimalRatio` API function
    - Update `updateHomeLeaveConfig` payload type for new request shape
    - Add emergency freeze toggle API function
    - _Requirements: 2.3, 4.5_

  - [x] 9.4 Add i18n translation keys for all new UI strings
    - Add Hebrew, English, and Russian translations for mode labels, feasibility messages, error messages, and slider labels
    - Use existing next-intl infrastructure
    - _Requirements: 11.1, 11.5, 11.6_

  - [ ]* 9.5 Write property test: Slider Monotonicity (fast-check)
    - **Property 3: Slider Monotonicity**
    - Generate random slider position pairs a < b (both in [0, 100])
    - Verify baseDays at position b <= baseDays at position a
    - **Validates: Requirements 3.2, 3.3**

  - [ ]* 9.6 Write property test: Emergency Freeze State Restoration (fast-check)
    - **Property 8: Emergency Freeze State Restoration**
    - Generate random pre-freeze states (mode, baseDays, homeDays, balanceValue)
    - Simulate activate → deactivate cycle
    - Verify all values are restored to pre-freeze state
    - **Validates: Requirements 7.1**

- [x] 10. Checkpoint - Frontend verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify mode switching, slider interaction, manual inputs, emergency freeze, RTL rendering, and feasibility feedback all work correctly.

- [ ] 11. Data migration verification and cleanup
  - [x] 11.1 Verify data migration on existing configurations
    - Test migration script on sample data with various `eligibility_threshold_hours` values
    - Verify rounding: values that don't divide evenly into days round to nearest whole day
    - Verify all migrated `base_days` and `home_days` are >= 1
    - Verify `min_rest_hours` is set to 0 for all migrated rows
    - _Requirements: 10.1, 10.3, 10.4_

  - [x] 11.2 Remove old `BalanceSlider.tsx` component and update imports
    - Delete `apps/web/components/home-leave/BalanceSlider.tsx`
    - Update any imports that referenced `BalanceSlider` to use new `RatioSlider`
    - _Requirements: 3.1_

  - [ ]* 11.3 Write property test: Migration Rounding Correctness (FsCheck)
    - **Property 11: Migration Rounding Correctness**
    - Generate random `eligibility_threshold_hours` values
    - Verify migration produces `base_days = max(1, round(eligibility_threshold_hours / 24))`
    - Verify all migrated values are positive integers
    - **Validates: Requirements 10.3**

  - [ ]* 11.4 Write property test: Mode Mutual Exclusivity (FsCheck)
    - **Property 1: Mode Mutual Exclusivity**
    - Generate random sequences of mode changes
    - Verify entity always has exactly one of Automatic or Manual as active mode
    - Verify EmergencyFreezeActive is independent of mode value
    - **Validates: Requirements 1.1**

- [x] 12. Final checkpoint - Full integration verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify end-to-end: mode switching persists correctly, solver payload is constructed per mode, emergency freeze activates/deactivates with state restoration, RTL slider renders correctly, and all migrated data is valid.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- FsCheck is used for C# property tests, fast-check for TypeScript frontend property tests
- The solver (`home_leave.py`) and solver Pydantic models (`solver_input.py`) require NO changes — all new behavior is achieved through parameter translation
- The `min_rest_hours` field is preserved in the database but always set to 0 for the new mode system
- Emergency freeze is implemented via payload manipulation, not a new solver mode
- The preview endpoint reuses the existing synchronous solver call pattern from the home-leave-slider spec

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.3"] },
    { "id": 1, "tasks": ["1.2", "1.4", "1.5"] },
    { "id": 2, "tasks": ["2.1", "2.2"] },
    { "id": 3, "tasks": ["2.3", "2.4", "4.1"] },
    { "id": 4, "tasks": ["4.2", "4.3", "4.4", "4.5"] },
    { "id": 5, "tasks": ["4.6", "4.7", "6.1"] },
    { "id": 6, "tasks": ["6.2", "6.3", "6.4", "6.5"] },
    { "id": 7, "tasks": ["8.1", "8.2", "8.3", "8.4", "8.5", "8.6"] },
    { "id": 8, "tasks": ["9.1", "9.2", "9.3", "9.4"] },
    { "id": 9, "tasks": ["9.5", "9.6", "11.1", "11.2"] },
    { "id": 10, "tasks": ["11.3", "11.4"] }
  ]
}
```
