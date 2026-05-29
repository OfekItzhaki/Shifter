# Implementation Plan: Shift Picker Lite

## Overview

This plan implements a lightweight `/pick` route within the existing Shifter Next.js web application. The route provides a focused, mobile-optimized experience for members to browse and request shifts from self-service scheduling groups. It reuses existing authentication, API client, and self-service tab components (SlotBrowserTab, MyShiftsTab) — the new code is primarily the route page, group selector, picker header, tab wrapper, i18n keys, and the localStorage-based last-group memory logic.

## Tasks

- [x] 1. Core utilities and last-group memory logic
  - [x] 1.1 Create last-group memory utility module
    - Create `apps/web/lib/utils/pickLastGroup.ts`
    - Define `LAST_GROUP_KEY = "shifter-pick-last-group"` constant
    - Implement `getLastGroup(): string | null` — reads from localStorage, returns null if missing/empty
    - Implement `setLastGroup(groupId: string): void` — stores group ID in localStorage
    - Implement `clearLastGroup(): void` — removes the key from localStorage
    - Implement `resolveLastGroup(storedGroupId: string | null, selfServiceGroups: GroupWithMemberCountDto[]): string | null` — validates UUID format via regex, checks group exists in the provided list, returns group ID or null
    - _Requirements: 3.1, 3.2, 3.4, 3.5_

  - [x]* 1.2 Write property test for last-group memory round-trip
    - **Property 1: Last group memory round-trip**
    - For any valid group ID that exists in the member's self-service groups list, `resolveLastGroup(groupId, groups)` returns the same group ID
    - **Validates: Requirements 3.1, 3.4**

  - [x]* 1.3 Write property test for invalid last-group memory clearing
    - **Property 2: Invalid last-group memory is cleared**
    - For any string that is not a valid UUID, or any valid UUID not in the groups list, `resolveLastGroup` returns null
    - **Validates: Requirements 3.2, 3.5**

- [x] 2. Group filtering and sorting logic
  - [x] 2.1 Create group filtering utility for the picker
    - Create `apps/web/lib/utils/pickGroupFilter.ts`
    - Implement `filterSelfServiceGroups(groups: GroupWithMemberCountDto[]): GroupWithMemberCountDto[]` — filters to `schedulingMode === "SelfService"` and sorts by name ascending using Hebrew locale (`localeCompare` with `"he"`)
    - _Requirements: 2.1_

  - [x]* 2.2 Write property test for group filtering
    - **Property 3: Group filtering preserves only self-service groups**
    - For any list of groups with mixed scheduling modes, the result contains exactly those with `schedulingMode === "SelfService"` and no others
    - **Validates: Requirements 2.1**

  - [x]* 2.3 Write property test for group sorting
    - **Property 4: Group list sorting is stable and locale-aware**
    - For any list of self-service groups, the sorted output is in ascending order by name using Hebrew locale comparison
    - **Validates: Requirements 2.1**

- [x] 3. Checkpoint — Utility layer complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. i18n messages for the picker
  - [x] 4.1 Add Hebrew i18n message keys for the pick namespace
    - Add `pick` namespace to `apps/web/messages/he.json` with keys: title, selectGroup, noGroups, tabs.slots, tabs.myShifts, refresh, back, memberCount, error, retry
    - Add corresponding keys to `apps/web/messages/en.json` and `apps/web/messages/ru.json`
    - All user-visible strings must be resolved from i18n keys — no inline hardcoded display text
    - _Requirements: 7.2, 7.5, 7.6, 8.1_

- [x] 5. PickerHeader component
  - [x] 5.1 Implement PickerHeader component
    - Create `apps/web/components/pick/PickerHeader.tsx`
    - Accept props: `groupName: string | null`, `onBack: () => void`, `onRefresh: () => void`, `refreshing: boolean`
    - Render a minimal mobile header with: group name text, back-navigation button (calls `onBack`), refresh button with loading spinner while `refreshing` is true
    - Use minimum 44x44px tap targets for back and refresh buttons
    - Use existing design tokens for colors and spacing
    - No sidebar navigation or desktop shell elements
    - Resolve all labels from `pick` i18n namespace
    - _Requirements: 6.3, 6.4, 6.5, 7.2_

- [x] 6. GroupSelector component
  - [x] 6.1 Implement GroupSelector component
    - Create `apps/web/components/pick/GroupSelector.tsx`
    - Accept props: `groups: GroupWithMemberCountDto[]`, `onSelect: (groupId: string, groupName: string) => void`
    - Render a list of group cards showing group name and member count
    - When groups array is empty, display the `pick.noGroups` localized message
    - On card tap, call `onSelect` with the group's ID and name
    - Use single-column layout, 44x44px minimum tap targets, 16px body text
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 6.1, 6.2, 6.7_

- [x] 7. PickerTabs component
  - [x] 7.1 Implement PickerTabs tab switcher component
    - Create `apps/web/components/pick/PickerTabs.tsx`
    - Accept props: `activeTab: "slots" | "my-shifts"`, `onTabChange: (tab: "slots" | "my-shifts") => void`
    - Render two tab buttons: "משמרות פנויות" (slots) and "המשמרות שלי" (my-shifts) resolved from i18n
    - Highlight active tab with visual indicator
    - Use 44x44px minimum tap targets
    - _Requirements: 5.1, 6.1_

- [x] 8. PickPage route component — orchestration and wiring
  - [x] 8.1 Create the /pick route page
    - Create `apps/web/app/pick/page.tsx` as the route page component
    - Implement the page state machine with phases: `loading`, `group-select`, `slot-browser`
    - On mount: check authentication via authStore — if not authenticated, redirect to `/login?redirect=/pick`
    - Fetch member's groups via existing groups API, filter to self-service using `filterSelfServiceGroups`
    - Read last-group from localStorage using `getLastGroup`, validate with `resolveLastGroup`
    - If valid last-group: set phase to `slot-browser` with that group
    - If no valid last-group: clear localStorage and set phase to `group-select`
    - Track `activeTab: "slots" | "my-shifts"` state for tab switching within slot-browser phase
    - _Requirements: 1.1, 1.3, 1.4, 3.1, 3.2, 3.5_

  - [x] 8.2 Wire group selection and last-group memory into PickPage
    - When member selects a group in GroupSelector: store group ID via `setLastGroup`, update state to `slot-browser` phase with selected group
    - When member taps back in PickerHeader: set phase to `group-select` (allow group switching)
    - When member switches group: update `Last_Group_Memory` to new group ID
    - _Requirements: 2.4, 3.3, 3.4_

  - [x] 8.3 Wire reused tab components into PickPage
    - In `slot-browser` phase, render `PickerTabs` for tab switching
    - When `activeTab === "slots"`: render `SlotBrowserTab` with `spaceId`, `groupId`, `isAdmin=false`
    - When `activeTab === "my-shifts"`: render `MyShiftsTab` with `spaceId`, `groupId`
    - Wire PickerHeader `onRefresh` to trigger data reload in the active tab (use key prop or callback)
    - Show `LoadingCard` skeleton placeholders while groups are loading
    - Show `ErrorRetry` component if groups fetch fails
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8, 4.9, 4.10, 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 6.6, 8.1, 8.2, 8.3, 8.4_

- [x] 9. Mobile layout and RTL styling
  - [x] 9.1 Apply mobile-optimized layout and RTL to the /pick route
    - Ensure the `/pick` page renders outside the main app shell (no sidebar)
    - Apply single-column layout filling viewport width with no horizontal scroll on screens < 640px
    - Inherit `dir="rtl"` from root HTML element — no additional RTL configuration needed
    - Set minimum font size of 16px for body text and 14px for secondary labels
    - Ensure all interactive elements have minimum 44x44px tap targets
    - Use existing application color scheme and design tokens
    - Display skeleton placeholders matching content layout while data loads
    - _Requirements: 6.1, 6.2, 6.3, 6.5, 6.6, 6.7, 7.1, 7.3, 7.4_

- [x] 10. Checkpoint — Full picker UI wired
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Slot sorting and capacity formatting property tests
  - [x]* 11.1 Write property test for slot sorting
    - **Property 5: Slot sorting is date-first then time-second**
    - For any list of available slots, sorting by date ascending then start time ascending produces a list where no slot appears before another with an earlier date, and within the same date no slot appears before another with an earlier start time
    - **Validates: Requirements 4.1**

  - [x]* 11.2 Write property test for capacity indicator format
    - **Property 6: Capacity indicator format**
    - For any slot with `currentFillCount` and `capacity` values, the capacity indicator renders as `"{currentFillCount}/{capacity}"`
    - **Validates: Requirements 4.2**

  - [x]* 11.3 Write property test for cancellation eligibility
    - **Property 7: Cancellation eligibility is time-based**
    - For any approved shift request and cancellation cutoff hours value, the cancel button is visible if and only if `shiftStartTime - currentTime > cutoffHours * 3600000`
    - **Validates: Requirements 5.4**

- [x] 12. Final checkpoint — All tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (Properties 1–7)
- The implementation uses TypeScript with the existing Next.js stack (React, next-intl, zustand, apiClient)
- All components follow existing RTL layout patterns — `dir="rtl"` is inherited from the root layout
- No new backend endpoints are needed — the picker reuses existing self-service API endpoints
- `SlotBrowserTab` and `MyShiftsTab` are reused directly with no modifications
- Error handling follows the existing pattern: `ErrorRetry` component for fetch failures, `MutationButton` pattern for mutations
- The `/pick` route renders outside the main app shell (no sidebar) with its own minimal header

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "2.1", "4.1"] },
    { "id": 1, "tasks": ["1.2", "1.3", "2.2", "2.3"] },
    { "id": 2, "tasks": ["5.1", "6.1", "7.1"] },
    { "id": 3, "tasks": ["8.1"] },
    { "id": 4, "tasks": ["8.2", "8.3"] },
    { "id": 5, "tasks": ["9.1"] },
    { "id": 6, "tasks": ["11.1", "11.2", "11.3"] }
  ]
}
```
