# Requirements Document

## Introduction

This feature introduces timezone-aware time display across the application. All time data is stored as UTC in the database. A new "User Settings" tab is added to the UI, consolidating settings currently scattered in the profile page and introducing a Country/State field. The system derives the user's timezone from their geographic selection and calculates the offset once per session at login time, applying it to all displayed time fields on the frontend.

## Glossary

- **System**: The Rolduler (Shifter) scheduling application as a whole
- **Settings_Service**: The backend service responsible for persisting and retrieving user settings (Application layer handler)
- **Timezone_Resolver**: The component that maps a Country/State selection to an IANA timezone identifier
- **Session_Context**: The frontend in-memory state established at login that holds the computed timezone offset for the duration of the session
- **User_Settings_Tab**: The new dedicated UI tab for user preferences, separated from the profile page
- **Profile_Page**: The existing page that currently contains personal info, notification preferences, time format, biometric login, export, and feedback sections
- **UTC**: Coordinated Universal Time — the canonical time representation stored in the database
- **Timezone_Offset**: The difference (in minutes) between UTC and the user's local time, derived from their Country/State selection

## Requirements

### Requirement 1: UTC Storage

**User Story:** As a developer, I want all time data stored as UTC in the database, so that time calculations are consistent regardless of user location.

#### Acceptance Criteria

1. THE System SHALL store all datetime fields in the database as UTC values
2. WHEN a datetime value is written to the database, THE System SHALL convert the value to UTC before persisting
3. WHEN a datetime value is read from the database, THE System SHALL return the raw UTC value without applying any timezone transformation at the API layer

### Requirement 2: User Settings — Country/State Field

**User Story:** As a user, I want to set my Country and optionally my State/Region in my settings, so that the system can determine my timezone automatically.

#### Acceptance Criteria

1. THE Settings_Service SHALL accept and persist a Country field (ISO 3166-1 alpha-2 code) on the User entity
2. THE Settings_Service SHALL accept and persist an optional State/Region field (ISO 3166-2 subdivision code) on the User entity
3. WHEN a user updates their Country or State field, THE Settings_Service SHALL validate the Country code against a known list of ISO 3166-1 alpha-2 codes
4. WHEN a user provides a State/Region code, THE Settings_Service SHALL validate that the subdivision code belongs to the selected Country
5. IF an invalid Country or State code is provided, THEN THE Settings_Service SHALL return a descriptive validation error

### Requirement 3: Timezone Resolution from Geography

**User Story:** As a user, I want the system to determine my timezone from my Country/State selection, so that I do not need to manually pick a timezone from a long list.

#### Acceptance Criteria

1. WHEN a user has a Country and optional State set, THE Timezone_Resolver SHALL map the geographic selection to a single IANA timezone identifier
2. WHEN a Country spans multiple timezones and no State is provided, THE Timezone_Resolver SHALL use the most populous timezone for that Country
3. WHEN a Country has a single timezone, THE Timezone_Resolver SHALL resolve to that timezone regardless of State
4. IF a user has no Country set, THEN THE Timezone_Resolver SHALL default to "Asia/Jerusalem" (the application's primary user base)

### Requirement 4: Session-Based Timezone Offset Calculation

**User Story:** As a user, I want the timezone offset calculated once when I log in, so that time display is fast and consistent throughout my session.

#### Acceptance Criteria

1. WHEN a user logs in successfully, THE System SHALL resolve the user's IANA timezone identifier and compute the current UTC offset in minutes
2. THE System SHALL include the computed timezone offset and IANA timezone identifier in the login response payload
3. THE Session_Context SHALL store the timezone offset for the duration of the session without recalculating
4. WHEN a user refreshes their token, THE System SHALL recalculate and return the current timezone offset (to account for DST changes between sessions)
5. WHEN a user updates their Country or State during an active session, THE Session_Context SHALL recalculate the timezone offset immediately using the new geographic selection

### Requirement 5: Frontend Time Display with Offset

**User Story:** As a user, I want all times displayed in my local timezone, so that I can understand schedule times without mental conversion.

#### Acceptance Criteria

1. WHILE a session is active, THE System SHALL apply the stored timezone offset to all datetime values before rendering them in the UI
2. THE System SHALL provide a centralized time formatting utility that accepts a UTC datetime and returns a localized display string using the session timezone
3. WHEN displaying a time value, THE System SHALL use the IANA timezone identifier (not a fixed numeric offset) to correctly handle DST transitions within displayed date ranges
4. THE System SHALL continue to send all datetime values to the API in UTC without applying any client-side offset to outgoing requests

### Requirement 6: User Settings Tab — UI Restructure

**User Story:** As a user, I want a dedicated Settings tab that groups all my preferences together, so that the profile page stays focused on personal identity information.

#### Acceptance Criteria

1. THE User_Settings_Tab SHALL be accessible from the main navigation as a separate route (/settings)
2. THE User_Settings_Tab SHALL contain the following sections moved from the Profile_Page: time format preference, notification preferences, push notification settings
3. THE User_Settings_Tab SHALL contain a new "Location" section with Country and State/Region selection fields
4. THE Profile_Page SHALL retain only personal identity information: display name, avatar, phone number, email, birthday, member since date, biometric login, data export, feedback, and account deletion
5. WHEN a user navigates to the User_Settings_Tab, THE System SHALL display the user's current Country, State, and resolved timezone as read-only confirmation text

### Requirement 7: Country/State Selection UI

**User Story:** As a user, I want an intuitive way to select my Country and State, so that timezone setup is quick and straightforward.

#### Acceptance Criteria

1. THE User_Settings_Tab SHALL display a searchable dropdown for Country selection showing country names in the user's preferred locale
2. WHEN a user selects a Country that has subdivisions relevant to timezone resolution, THE User_Settings_Tab SHALL display a secondary searchable dropdown for State/Region
3. WHEN a user selects a Country with a single timezone, THE User_Settings_Tab SHALL hide the State/Region dropdown and display the resolved timezone immediately
4. WHEN a user changes their Country selection, THE User_Settings_Tab SHALL clear the previously selected State/Region
5. THE User_Settings_Tab SHALL display the resolved timezone name (e.g., "Asia/Jerusalem", "America/New_York") as confirmation after selection
