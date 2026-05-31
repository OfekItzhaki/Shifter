# Requirements Document

## Introduction

Display the rest duration (gap between consecutive assignments) for each person inline in the schedule view. This helps admins quickly identify whether team members have adequate rest between shifts. The calculation is performed entirely on the frontend using already-loaded assignment data, with color-coding to highlight potential fatigue risks relative to the group's configured minimum rest threshold.

## Glossary

- **Rest_Duration_Display**: The UI element showing the computed gap between a person's consecutive assignments, rendered inline in the schedule table.
- **Schedule_View**: The ScheduleTaskTable component that renders assignments grouped by task for a selected day.
- **Assignment**: A single shift assignment containing personId, personName, taskTypeName, slotStartsAt, and slotEndsAt UTC timestamps.
- **Min_Rest_Threshold**: The group-level `minRestBetweenShiftsHours` setting that defines the minimum acceptable rest period between consecutive assignments for the same person.
- **Rest_Calculator**: The frontend utility that computes the time gap between a person's consecutive assignments across all tasks.
- **Admin_User**: A user with `isAdmin = true` in the current group context (includes Admin, GroupOwner, and SpaceOwner permission levels).

## Requirements

### Requirement 1: Compute Rest Duration Between Consecutive Assignments

**User Story:** As an admin, I want to see how much rest each person has before their next assignment, so that I can identify potential fatigue risks.

#### Acceptance Criteria

1. WHEN assignment data is loaded, THE Rest_Calculator SHALL compute the time gap between each person's assignment end time (`slotEndsAt`) and their next assignment start time (`slotStartsAt`) across all tasks in the schedule.
2. THE Rest_Calculator SHALL sort all assignments for a given person chronologically by `slotStartsAt` before computing gaps.
3. THE Rest_Calculator SHALL use the user's configured timezone (`timezoneId` from auth store) when converting UTC timestamps for display purposes.
4. THE Rest_Calculator SHALL express the rest duration in days and hours (e.g., "1d 4h") when the gap is 24 hours or more, and in hours only (e.g., "8h") when the gap is less than 24 hours.
5. IF a person has no subsequent assignment in the loaded schedule data, THEN THE Rest_Duration_Display SHALL not render a rest indicator for that assignment.

### Requirement 2: Restrict Visibility to Admin Users

**User Story:** As a product owner, I want rest duration information visible only to admins, so that regular members do not see operational details about other people's schedules.

#### Acceptance Criteria

1. WHILE `isAdmin` is false, THE Schedule_View SHALL not render any Rest_Duration_Display elements.
2. WHILE `isAdmin` is true, THE Schedule_View SHALL render Rest_Duration_Display elements for each assignment that has a subsequent assignment for the same person.
3. THE Schedule_View SHALL evaluate the `isAdmin` prop already passed to the ScheduleTaskTable component to determine visibility.

### Requirement 3: Display Rest Duration Inline in Schedule Table

**User Story:** As an admin, I want rest duration shown subtly inline with each assignment row, so that I can see the information without the UI becoming cluttered.

#### Acceptance Criteria

1. THE Rest_Duration_Display SHALL render as a small, secondary-styled label adjacent to or below the person's assignment row in the ScheduleTaskTable.
2. THE Rest_Duration_Display SHALL use a font size and weight that is visually subordinate to the primary assignment information (person name, task, time range).
3. THE Rest_Duration_Display SHALL include a localized label suffix (e.g., "rest" / "מנוחה" / "отдых") following the duration value.

### Requirement 4: Color-Code Based on Min-Rest Threshold

**User Story:** As an admin, I want rest durations color-coded relative to the minimum rest threshold, so that I can instantly spot assignments with insufficient rest.

#### Acceptance Criteria

1. WHEN the computed rest duration is below the Min_Rest_Threshold, THE Rest_Duration_Display SHALL render with a red color indicator (e.g., `text-red-600`).
2. WHEN the computed rest duration equals the Min_Rest_Threshold exactly, THE Rest_Duration_Display SHALL render with an amber color indicator (e.g., `text-amber-600`).
3. WHEN the computed rest duration exceeds the Min_Rest_Threshold, THE Rest_Duration_Display SHALL render with a neutral/green color indicator (e.g., `text-slate-500` or `text-emerald-600`).
4. THE Rest_Duration_Display SHALL accept the Min_Rest_Threshold value (in hours) as a prop from the parent component.

### Requirement 5: Cross-Task Calculation

**User Story:** As an admin, I want rest duration calculated across all tasks, so that I see the true rest gap regardless of which task the person is assigned to next.

#### Acceptance Criteria

1. THE Rest_Calculator SHALL consider all assignments for a person across all task types (תלים) in the loaded schedule when determining the next consecutive assignment.
2. WHEN a person has assignments in multiple tasks, THE Rest_Calculator SHALL identify the chronologically next assignment regardless of task type.
3. THE Rest_Calculator SHALL not limit gap calculation to assignments within the same task group.

### Requirement 6: Internationalization Support

**User Story:** As a user viewing the app in Hebrew, English, or Russian, I want rest duration labels displayed in my selected language.

#### Acceptance Criteria

1. THE Rest_Duration_Display SHALL use `next-intl` translation keys for all user-facing text including the "rest" label suffix and duration unit abbreviations.
2. THE Rest_Duration_Display SHALL support the three application locales: Hebrew (he), English (en), and Russian (ru).
3. THE Rest_Duration_Display SHALL format duration values using locale-appropriate abbreviations (e.g., "ש" for hours in Hebrew, "h" in English, "ч" in Russian; "י" for days in Hebrew, "d" in English, "д" in Russian).

### Requirement 7: Frontend-Only Implementation

**User Story:** As a developer, I want this feature implemented entirely on the frontend with no backend changes, so that deployment is simple and the existing API contract is preserved.

#### Acceptance Criteria

1. THE Rest_Calculator SHALL compute rest durations using only the assignment data already available in the client-side schedule state (the `assignments` array from `ScheduleVersionDetailDto`).
2. THE Rest_Duration_Display SHALL not trigger any additional API calls to render rest information.
3. THE Rest_Calculator SHALL perform all timestamp arithmetic on the client side using the UTC `slotStartsAt` and `slotEndsAt` values from `AssignmentDto`.
