# Requirements Document

## Introduction

This feature adds a lightweight "shift picker" route to the existing Shifter Next.js web application. The route provides a focused, mobile-optimized experience for members to browse and request shifts from self-service scheduling groups. It reuses the existing authentication system, self-service API endpoints, and core UI components (SlotBrowserTab, MyShiftsTab) already built in the main app.

The shift picker is NOT a separate application — it is a new route (e.g., `/pick`) within the same codebase and deployment. It targets members who primarily use phones and need a streamlined flow: select a self-service group → browse available slots → request shifts → view current requests and approved shifts. No admin tools, alerts, messages, or group creation are included.

## Glossary

- **Shift_Picker_View**: The new mobile-optimized route that provides the streamlined shift picking experience
- **Group_Selector**: The UI component that lists the member's self-service groups and allows selection
- **Slot_Browser**: The existing SlotBrowserTab component adapted for the shift picker layout, displaying available shift slots
- **My_Shifts_Panel**: The existing MyShiftsTab component adapted for the shift picker layout, showing approved and pending shifts
- **Last_Group_Memory**: The localStorage-based mechanism that remembers the member's last selected group
- **Self_Service_Group**: A group with `schedulingMode === "SelfService"` that allows members to pick their own shifts
- **Member**: An authenticated user who belongs to one or more self-service groups
- **API_Client**: The existing `apiClient` module used for all HTTP requests to the backend

## Requirements

### Requirement 1: Route Registration and Access Control

**User Story:** As a member, I want to access the shift picker at a dedicated URL, so that I can quickly navigate to it from my phone's home screen or bookmarks.

#### Acceptance Criteria

1. THE Shift_Picker_View SHALL be accessible at the `/pick` route within the existing Next.js application
2. WHEN an unauthenticated user navigates to `/pick`, THE Shift_Picker_View SHALL redirect the user to the existing login page with a return URL pointing back to `/pick`
3. WHEN an authenticated member navigates to `/pick`, THE Shift_Picker_View SHALL render the shift picker interface without requiring additional authentication
4. THE Shift_Picker_View SHALL share the same JWT-based authentication tokens and session management as the rest of the application

### Requirement 2: Self-Service Group Selection

**User Story:** As a member, I want to see only my self-service groups listed, so that I can quickly pick which group to browse shifts for.

#### Acceptance Criteria

1. WHEN the Shift_Picker_View loads and no Last_Group_Memory exists, THE Group_Selector SHALL display a list of all groups the authenticated member belongs to that have `schedulingMode` equal to `SelfService`, sorted by group name ascending
2. THE Group_Selector SHALL display each group's name and member count
3. WHEN the member belongs to zero groups with `schedulingMode` equal to `SelfService`, THE Group_Selector SHALL display a message indicating no self-service groups are available
4. WHEN the member selects a group, THE Group_Selector SHALL store the group ID in Last_Group_Memory and navigate to the slot browsing view for that group

### Requirement 3: Last Group Memory and Quick Return

**User Story:** As a returning member, I want the app to remember my last selected group, so that I go straight to my shifts without re-selecting every time.

#### Acceptance Criteria

1. WHEN the Shift_Picker_View loads and a Last_Group_Memory value exists that corresponds to a Self_Service_Group of which the authenticated member is currently a member, THE Shift_Picker_View SHALL skip the Group_Selector and display the slot browsing view for the remembered group
2. IF the remembered group no longer exists, or the member is no longer a member of that group, or the group's schedulingMode is no longer equal to "SelfService", THEN THE Shift_Picker_View SHALL clear the Last_Group_Memory and display the Group_Selector
3. THE Shift_Picker_View SHALL provide a visible back button or group-switch control that allows the member to return to the Group_Selector from the slot browsing view
4. WHEN the member switches to a different group via the Group_Selector, THE Last_Group_Memory SHALL update to the newly selected group ID
5. IF the Last_Group_Memory value is missing, empty, or does not match a valid group ID format, THEN THE Shift_Picker_View SHALL clear the Last_Group_Memory and display the Group_Selector

### Requirement 4: Slot Browsing in Picker View

**User Story:** As a member, I want to browse available shift slots for my selected group, so that I can choose shifts that fit my schedule.

#### Acceptance Criteria

1. WHEN the member enters the slot browsing view, THE Slot_Browser SHALL call the GET available-slots API endpoint and display all slots with remaining capacity, sorted by date ascending then start time ascending
2. THE Slot_Browser SHALL display for each slot: date, Hebrew day-of-week name, start time, end time, task name, and a capacity indicator showing current fill versus total capacity (formatted as "current/total", e.g. "3/5")
3. WHILE the request window is open, THE Slot_Browser SHALL display a "Request" button on each slot that has remaining capacity, and a "Join Waitlist" button on each slot that is at full capacity
4. WHILE the request window is closed, THE Slot_Browser SHALL hide the "Request" and "Join Waitlist" buttons, display a banner indicating requests are not currently accepted, and show the next window opening date and time
5. WHEN a member taps the "Request" button, THE Slot_Browser SHALL call the POST shift-requests API endpoint, disable the button until the response is received, and on success update the slot capacity indicator to reflect the new fill count
6. IF the shift request is rejected due to full capacity, THEN THE Slot_Browser SHALL display the rejection reason to the member and show up to 5 alternative available slots for the same day returned by the API; if no alternatives are returned, THE Slot_Browser SHALL display only the rejection reason
7. IF the shift request is rejected due to max shifts reached, THEN THE Slot_Browser SHALL display a message indicating the member has reached their maximum shift count for the cycle
8. WHEN a member taps "Join Waitlist", THE Slot_Browser SHALL call the POST join-waitlist API endpoint, disable the button until the response is received, and on success display a confirmation with the assigned queue position
9. IF the waitlist join is rejected due to duplicate entry, THEN THE Slot_Browser SHALL display a message indicating the member is already on the waitlist for that slot
10. IF the GET available-slots API call fails, THEN THE Slot_Browser SHALL display an error message indicating slots could not be loaded and provide a retry action

### Requirement 5: My Shifts View in Picker

**User Story:** As a member, I want to view my approved shifts and pending requests within the picker, so that I can see my schedule without navigating to the full app.

#### Acceptance Criteria

1. THE Shift_Picker_View SHALL provide a tab or toggle to switch between the Slot_Browser and the My_Shifts_Panel
2. WHEN the member switches to the My_Shifts_Panel, THE My_Shifts_Panel SHALL display all shift requests for the current cycle grouped by status in the following order: approved, pending, and cancelled
3. THE My_Shifts_Panel SHALL display for each shift: date, Hebrew day-of-week name, start time in 24-hour format, end time in 24-hour format, task name, and a color-coded status badge
4. WHEN a member has an approved shift whose start time is more than CancellationCutoffHours in the future, THE My_Shifts_Panel SHALL display a "Cancel" button next to that shift
5. WHEN the member taps "Cancel", THE My_Shifts_Panel SHALL show a dialog requiring a cancellation reason between 1 and 500 characters before calling the cancel-request API endpoint
6. IF the cancellation API returns an error indicating the cancellation window has closed, THEN THE My_Shifts_Panel SHALL display an error message indicating the cancellation is no longer permitted and hide the "Cancel" button for that shift
7. THE My_Shifts_Panel SHALL display the member's current approved shift count as a fraction of the configured maximum for the cycle (e.g., "3 / 5")
8. WHEN the member's approved shift count is below the configured minimum for the cycle, THE My_Shifts_Panel SHALL display a warning indicating the member is under-scheduled

### Requirement 6: Mobile-Optimized Layout

**User Story:** As a member using a phone, I want the shift picker to be optimized for small screens and touch interaction, so that I can pick shifts comfortably on my device.

#### Acceptance Criteria

1. THE Shift_Picker_View SHALL use a single-column layout that fills the viewport width with no horizontal scrolling on screens narrower than 640px
2. THE Shift_Picker_View SHALL use touch-friendly tap targets with a minimum size of 44x44 CSS pixels for all interactive elements
3. THE Shift_Picker_View SHALL render without the sidebar navigation or desktop shell, presenting only the shift picker content and a header containing the group name and a back-navigation control
4. THE Shift_Picker_View SHALL display a visible refresh button that reloads slot data, and SHALL show a loading indicator on the button while the refresh request is in progress
5. THE Shift_Picker_View SHALL use the existing application color scheme and design tokens for visual consistency
6. WHILE data is loading, THE Shift_Picker_View SHALL display skeleton placeholders that match the expected content layout
7. THE Shift_Picker_View SHALL use a minimum font size of 16px for body text and 14px for secondary labels to ensure readability without zooming on mobile devices

### Requirement 7: RTL and Hebrew Localization

**User Story:** As a Hebrew-speaking member, I want the shift picker to display in RTL with Hebrew text, so that the interface is natural and readable.

#### Acceptance Criteria

1. THE Shift_Picker_View SHALL render in RTL layout direction by inheriting the `dir="rtl"` attribute set on the root HTML element by the application layout
2. THE Shift_Picker_View SHALL use Hebrew text for all labels, buttons, status badges, and messages by resolving every user-visible string from keys defined in the next-intl Hebrew message file, with no inline hardcoded display text
3. THE Shift_Picker_View SHALL display dates using Hebrew day-of-week names and format full dates using the he-IL locale convention (day-month-year order)
4. THE Shift_Picker_View SHALL display time values in 24-hour HH:mm format
5. IF an API error occurs and a specific Hebrew error message key exists for that error type, THEN THE Shift_Picker_View SHALL display the corresponding localized Hebrew error message
6. IF an API error occurs and no specific Hebrew error message key exists for that error type, THEN THE Shift_Picker_View SHALL display a generic Hebrew error message indicating the operation failed

### Requirement 8: Error and Empty States

**User Story:** As a member, I want clear feedback when something goes wrong or when there is no data, so that I understand what is happening and what I can do.

#### Acceptance Criteria

1. IF a data fetch fails, THEN THE Shift_Picker_View SHALL display a localized Hebrew error message with a "נסה שוב" (Retry) button that re-triggers the failed request
2. WHEN no slots are available for the current cycle, THE Slot_Browser SHALL display a message indicating no shifts are available at this time
3. WHILE a mutation (shift request, cancel request, join waitlist) is in progress, THE Shift_Picker_View SHALL disable the triggering button and show a loading indicator within that button to prevent double-submission
4. IF a mutation fails, THEN THE Shift_Picker_View SHALL display the localized error message mapped from the API response error code, re-enable the triggering button, and revert any optimistic UI updates to reflect the state prior to the mutation attempt
