# Implementation Plan: Self-Service Scheduling UI

## Overview

This plan implements the frontend UI for the self-service scheduling system in the Shifter web application. The backend is fully implemented — this spec covers the Next.js components, API client module, validation/formatting utilities, i18n messages, and tab integration needed to expose all self-service functionality to members and admins. Tasks are ordered to build the API client and utilities first, then individual tab components, then the group creation wizard extension, and finally integration wiring.

## Tasks

- [x] 1. API client module and TypeScript types
  - [x] 1.1 Create self-service API client module with all typed functions
    - Create `lib/api/selfService.ts` with all TypeScript interfaces (ShiftTemplateDto, SelfServiceConfigDto, AvailableSlotDto, AvailableSlotsResponse, ShiftRequestDto, MyShiftsResponse, WaitlistEntryDto, SwapRequestDto) and payload types
    - Implement all async API functions using the existing `apiClient` instance: listShiftTemplates, createShiftTemplate, updateShiftTemplate, deleteShiftTemplate, getSelfServiceConfig, updateSelfServiceConfig, getAvailableSlots, submitShiftRequest, cancelShiftRequest, getMyShiftRequests, joinWaitlist, leaveWaitlist, acceptWaitlistOffer, getMyWaitlistEntries, proposeSwap, acceptSwap, declineSwap, cancelSwap, getMySwaps, adminAssignMember, adminRemoveMember
    - All functions must include spaceId and groupId in the URL path following the pattern `/spaces/{spaceId}/groups/{groupId}/...`
    - _Requirements: 11.1, 11.2, 11.3, 11.4_

  - [ ]* 1.2 Write property test for API client URL construction
    - **Property 15: API client URLs contain spaceId and groupId**
    - **Validates: Requirements 11.3**

- [x] 2. Validation and formatting utilities
  - [x] 2.1 Create self-service validation utilities
    - Create `lib/utils/selfServiceValidation.ts` with functions: `validateTemplateTimeRange`, `validateSelfServiceConfig`, `validateCancellationReason`
    - `validateTemplateTimeRange`: reject when startTime >= endTime
    - `validateSelfServiceConfig`: reject when minShiftsPerCycle > maxShiftsPerCycle, or any field outside valid range (min: 0-100, max: 1-100, offsets: 1-720, waitlistOfferMinutes: 1-1440, cycleDurationDays: 1-365)
    - `validateCancellationReason`: reject when reason is empty or exceeds 500 characters
    - Return `{ valid: boolean, errorKey?: string }` with i18n error keys
    - _Requirements: 3.3, 4.3, 4.4, 6.4_

  - [ ]* 2.2 Write property tests for validation functions
    - **Property 1: Shift template time validation rejects invalid ranges**
    - **Property 2: Self-service config validation rejects out-of-range values**
    - **Validates: Requirements 3.3, 4.3, 4.4**

  - [x] 2.3 Create self-service formatting utilities
    - Create `lib/utils/selfServiceFormat.ts` with functions: `formatSlotDate`, `formatTime24h`, `formatCountdown`, `getCapacityClass`
    - `HEBREW_DAY_NAMES` constant array indexed by JS getDay() (0=Sunday)
    - `formatSlotDate`: return Hebrew locale date string with day name
    - `formatTime24h`: return HH:mm format with no AM/PM
    - `formatCountdown`: calculate remaining hours/minutes (or days/hours if > 24h) from expiresAt timestamp
    - `getCapacityClass`: return "high-availability" if remaining > 50%, otherwise "nearly-full"
    - _Requirements: 5.2, 5.10, 10.3, 10.4_

  - [ ]* 2.4 Write property tests for formatting functions
    - **Property 11: Countdown timer displays correct remaining time**
    - **Property 12: Hebrew date formatting produces Hebrew day names**
    - **Property 13: Time formatting uses 24-hour format**
    - **Validates: Requirements 10.3, 10.4**

- [x] 3. i18n messages and error mapping
  - [x] 3.1 Add Hebrew and English i18n message keys for self-service UI
    - Add `selfService` namespace to `apps/web/messages/he.json` with all keys: modeSelector, confirmDialog, tabs, slotBrowser, myShifts, waitlist, swaps, templates, config, adminOverrides, errors, loading, error, retry
    - Add corresponding keys to `apps/web/messages/en.json`
    - Include all button labels, form labels, validation messages, error messages, and status texts
    - _Requirements: 10.1, 10.2, 10.5_

  - [x] 3.2 Create error code mapping utility
    - Create error code to i18n key mapping in the self-service module
    - Map known backend error slugs to Hebrew messages
    - Provide generic Hebrew fallback for unknown error codes
    - For 422 ProblemDetails responses, display the `detail` field directly
    - _Requirements: 10.5, 12.1, 12.2_

  - [ ]* 3.3 Write property test for error code mapping
    - **Property 14: Error code mapping produces Hebrew messages**
    - **Validates: Requirements 10.5**

- [x] 4. Checkpoint — Foundation layer complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Slot Browser tab component
  - [x] 5.1 Implement SlotBrowserTab component
    - Create `SlotBrowserTab` component that fetches available slots via `getAvailableSlots`
    - Display slots sorted by date ascending, then start time ascending
    - Render each slot with: date, Hebrew day name, start time (24h), end time (24h), task name, capacity indicator (e.g., "2/5")
    - Apply capacity CSS class via `getCapacityClass` (high-availability vs nearly-full)
    - Show "Request" button for slots with remaining capacity
    - Show "Join Waitlist" button for full slots (currentFillCount === capacity)
    - Show request window closed banner with next opening time when window is closed
    - Include date filter for viewing specific days
    - Handle loading skeleton, error state with retry button
    - On "Request" click: call `submitShiftRequest`, update capacity on success, show error on rejection with alternative slots
    - On "Join Waitlist" click: call `joinWaitlist`, show position on success
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 5.10_

  - [ ]* 5.2 Write property tests for slot browser display logic
    - **Property 3: Slot browser displays slots in correct sort order**
    - **Property 4: Slot display includes all required fields**
    - **Property 5: Full slots show "Join Waitlist" instead of "Request"**
    - **Property 6: Capacity visual distinction at 50% threshold**
    - **Validates: Requirements 5.1, 5.2, 5.8, 5.10**

- [x] 6. My Shifts tab component
  - [x] 6.1 Implement MyShiftsTab component
    - Create `MyShiftsTab` component that fetches shift requests via `getMyShiftRequests`
    - Group requests by status: approved first, then pending, then cancelled
    - Render each request with: date, Hebrew day name, start time, end time, task name, color-coded status badge
    - Show "Cancel" button on approved shifts within cancellation window
    - On "Cancel" click: show dialog requiring reason (1-500 chars), validate with `validateCancellationReason`, call `cancelShiftRequest`
    - Display shift count relative to min/max (e.g., "3 / 5 shifts this cycle")
    - Show under-scheduled warning when count < min
    - Handle loading skeleton, error state with retry button
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7_

  - [ ]* 6.2 Write property tests for my shifts display logic
    - **Property 7: Shift requests are grouped by status**
    - **Property 8: Shift request display includes all required fields**
    - **Validates: Requirements 6.1, 6.2**

- [x] 7. Waitlist tab component
  - [x] 7.1 Implement WaitlistTab component
    - Create `WaitlistTab` component that fetches waitlist entries via `getMyWaitlistEntries`
    - Render each entry with: slot date, start time, end time, task name, queue position, status
    - Highlight offered entries prominently with countdown timer (using `formatCountdown`) and "Accept" / "Decline" buttons
    - On "Accept" click: call `acceptWaitlistOffer`, handle max-shifts rejection
    - On "Decline" click: update entry status
    - Show "Leave" button with confirmation prompt for waiting entries
    - On "Leave" click: call `leaveWaitlist`
    - Handle loading skeleton, error state with retry button
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 7.7_

  - [ ]* 7.2 Write property test for waitlist display logic
    - **Property 9: Waitlist entry display includes all required fields**
    - **Validates: Requirements 7.1**

- [x] 8. Swaps tab component
  - [x] 8.1 Implement SwapsTab component
    - Create `SwapsTab` component that fetches swap requests via `getMySwaps`
    - Render each swap with: status, counterpart name, offered shift details (date + time + task), requested shift details (date + time + task)
    - Show countdown timer for pending swaps (72h expiry) using `formatCountdown`
    - Implement "Propose Swap" flow: member selects one of their approved shifts to offer, then selects another member's approved shift to request
    - On propose: call `proposeSwap`, show error on rejection (duplicate, invalid ownership)
    - Show "Accept" / "Decline" buttons on incoming proposals
    - On accept: call `acceptSwap`, handle conflict rejection with details
    - On decline: call `declineSwap`
    - Show "Cancel" button on outgoing pending swaps
    - On cancel: call `cancelSwap`
    - Handle loading skeleton, error state with retry button
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10_

  - [ ]* 8.2 Write property test for swaps display logic
    - **Property 10: Swap request display includes all required fields**
    - **Validates: Requirements 8.1**

- [x] 9. Checkpoint — Member tab components complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Shift Templates tab component (Admin)
  - [x] 10.1 Implement ShiftTemplatesTab component
    - Create `ShiftTemplatesTab` component that fetches templates via `listShiftTemplates`
    - Display list showing: day of week (Hebrew name), start time, end time, required headcount, task name
    - Provide create form with: day of week dropdown (Sunday-Saturday), time pickers, headcount number input (1-999), task dropdown
    - Validate with `validateTemplateTimeRange` before submission
    - On create: call `createShiftTemplate`, add to list on success
    - Provide edit functionality: inline or modal editing, call `updateShiftTemplate` on save
    - Provide delete with confirmation dialog, call `deleteShiftTemplate` on confirm
    - Show API error messages on create/update/delete failure
    - Handle loading skeleton, error state with retry button
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

- [x] 11. Self-Service Config tab component (Admin)
  - [x] 11.1 Implement SelfServiceConfigTab component
    - Create `SelfServiceConfigTab` component that fetches config via `getSelfServiceConfig`
    - Display editable inputs for: minShiftsPerCycle, maxShiftsPerCycle, requestWindowOpenOffsetHours, requestWindowCloseOffsetHours, cancellationCutoffHours, waitlistOfferMinutes, cycleDurationDays
    - Use appropriate number inputs with min/max HTML constraints
    - Validate with `validateSelfServiceConfig` before submission
    - On save: call `updateSelfServiceConfig`, show success confirmation
    - Show API error messages on failure, preserve user input for correction
    - Handle loading skeleton, error state with retry button
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

- [x] 12. Admin Overrides tab component
  - [x] 12.1 Implement AdminOverridesTab component
    - Create `AdminOverridesTab` component that displays current cycle's shift slots with assigned members and capacity
    - Provide "Assign Member" action: member picker dropdown filtered to group members not already on the slot
    - On assign: call `adminAssignMember`, update slot display on success
    - Provide "Remove" action next to each assigned member
    - On remove with confirmation: call `adminRemoveMember`, update slot display on success
    - Show permission denied message and disable actions if user lacks `SchedulePublish` permission
    - Show API error messages (member not in group, already assigned, not assigned)
    - Handle loading skeleton, error state with retry button
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7_

- [x] 13. Checkpoint — Admin tab components complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 14. Group creation wizard extension
  - [x] 14.1 Extend CreateGroupWizard with scheduling mode selection step
    - Add `SchedulingModeSelector` component presenting two visual cards: "סידור אוטומטי" and "בחירת משמרות"
    - Each card displays a description explaining the mode
    - Show prominent warning that the choice is permanent
    - Highlight selected card with distinct border and background
    - Disable continue button until a mode is selected
    - When "סידור אוטומטי" selected: show existing template picker and home-leave toggle
    - When "בחירת משמרות" selected: show self-service-specific template options
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7_

  - [x] 14.2 Implement ModeWarningDialog and wizard submission
    - Create `ModeWarningDialog` component with irreversibility warning and confirm/cancel buttons
    - Show dialog after user clicks continue with mode and template selected
    - On confirm: call group creation API with selected `schedulingMode` and template config
    - On API error: display error message, allow retry without losing selections
    - _Requirements: 1.8, 1.9, 1.10_

- [x] 15. Mode-conditional tab navigation
  - [x] 15.1 Extend GroupDetailPage tab navigation for scheduling mode
    - Extend `ActiveTab` type with self-service tab values: "available-slots", "my-shifts", "waitlist", "swaps", "shift-templates", "self-service-config", "admin-overrides"
    - Define tab arrays: `AUTO_GENERATED_TABS`, `SELF_SERVICE_MEMBER_TABS`, `SELF_SERVICE_ADMIN_TABS`
    - Render tabs conditionally based on group's `schedulingMode` and user's admin status
    - Default to "available-slots" for self-service groups, "schedule" for auto-generated groups
    - Hide solver-related tabs (constraints, tasks, live-status, stats) for self-service groups
    - Hide admin-only tabs (shift-templates, self-service-config, admin-overrides) for non-admin members
    - Lazy-load each self-service tab component following existing pattern
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 16. Loading and error state components
  - [x] 16.1 Implement shared loading and error components for self-service tabs
    - Create or reuse `LoadingCard` component with skeleton rows matching tab layouts
    - Create or reuse `ErrorRetry` component with Hebrew error message and "נסה שוב" retry button
    - Ensure mutation buttons show spinner and disable during in-flight requests
    - Ensure mutations refetch affected data on success
    - Ensure mutations restore previous state and show error on failure
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

- [x] 17. Final checkpoint — Full UI integration
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (Properties 1–15)
- Unit tests validate specific examples and edge cases
- The implementation uses TypeScript with the existing Next.js stack (React, next-intl, zustand, apiClient)
- All components follow existing RTL layout patterns — no additional RTL configuration needed
- The backend is fully implemented; this spec only covers frontend components and API client integration
- Error handling follows the existing pattern: 422 ProblemDetails `detail` displayed directly, other errors mapped via i18n

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "2.3", "3.1"] },
    { "id": 1, "tasks": ["1.2", "2.2", "2.4", "3.2"] },
    { "id": 2, "tasks": ["3.3", "5.1", "6.1", "7.1", "8.1"] },
    { "id": 3, "tasks": ["5.2", "6.2", "7.2", "8.2", "10.1", "11.1", "12.1"] },
    { "id": 4, "tasks": ["14.1", "15.1", "16.1"] },
    { "id": 5, "tasks": ["14.2"] }
  ]
}
```
