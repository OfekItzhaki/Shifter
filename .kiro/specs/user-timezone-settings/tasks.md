# Implementation Plan: User Timezone Settings

## Overview

This plan implements timezone-aware time display by adding geographic location fields to the User entity, a timezone resolver that maps Country/State to IANA timezone identifiers, session-scoped offset delivery via login/refresh responses, a centralized frontend formatting utility, and a new Settings page that consolidates user preferences.

## Tasks

- [x] 1. Backend domain and infrastructure — timezone resolution
  - [x] 1.1 Add CountryCode and StateCode to User entity and create database migration
    - Add `CountryCode` (string?, ISO 3166-1 alpha-2) and `StateCode` (string?, ISO 3166-2) properties to the `User` entity in the Domain layer
    - Add `UpdateLocation(string? countryCode, string? stateCode)` method to User entity
    - Create EF Core migration adding `country_code VARCHAR(2) NULL` and `state_code VARCHAR(6) NULL` columns to the users table
    - Configure columns via Fluent API in the Infrastructure layer
    - _Requirements: 2.1, 2.2_

  - [x] 1.2 Implement ITimezoneResolver interface and TimezoneResolver
    - Define `ITimezoneResolver` interface in the Application layer with `Resolve(string? countryCode, string? stateCode)` returning `TimezoneResolution(string IanaTimezoneId, int OffsetMinutes)`
    - Implement `TimezoneResolver` in Infrastructure using a static country-timezone mapping dictionary
    - Implement fallback chain: State → Country (most populous TZ) → `Asia/Jerusalem`
    - Use `TimeZoneInfo` to compute current UTC offset in minutes for the resolved IANA ID
    - Register `ITimezoneResolver` in DI container
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 1.3 Create CountryTimezoneMap static data
    - Create a static class with a dictionary mapping `(CountryCode, StateCode?)` → IANA timezone ID
    - Include all single-timezone countries with their IANA ID
    - Include multi-timezone countries (US, RU, AU, CA, BR, etc.) with state-level mappings
    - Define most-populous-timezone fallback for each multi-timezone country
    - _Requirements: 3.1, 3.2, 3.3_

  - [x]* 1.4 Write property tests for TimezoneResolver
    - **Property 4: Timezone Resolver Output Validity** — Generate random valid country/state inputs, verify output is always a valid IANA timezone identifier
    - **Property 5: Single-Timezone Country Invariant** — For single-TZ countries, generate random state values, verify timezone output is constant
    - **Property 6: Offset Computation Correctness** — Generate random IANA timezone IDs and timestamps, verify computed offset matches expected
    - **Validates: Requirements 3.1, 3.3, 4.1**

- [x] 2. Backend application layer — user settings commands and queries
  - [x] 2.1 Implement UpdateUserLocationCommand and handler
    - Create `UpdateUserLocationCommand(Guid UserId, string CountryCode, string? StateCode)` record
    - Create `UpdateUserLocationValidator` using FluentValidation to validate CountryCode against ISO 3166-1 alpha-2 list and StateCode against the country's subdivisions
    - Implement `UpdateUserLocationHandler` that persists location to User entity and calls `ITimezoneResolver.Resolve` to return the new timezone
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 4.5_

  - [x] 2.2 Implement GetUserSettingsQuery and handler
    - Create `GetUserSettingsQuery(Guid UserId)` record
    - Create `UserSettingsDto` with CountryCode, StateCode, TimezoneId, TimezoneOffsetMinutes, TimeFormat
    - Implement handler that reads user settings and resolves current timezone
    - _Requirements: 6.5_

  - [x]* 2.3 Write property tests for settings persistence and validation
    - **Property 2: User Settings Persistence Round-Trip** — Generate random valid ISO country/state pairs, persist and read back, verify equality
    - **Property 3: Geographic Code Validation** — Generate random strings, verify only valid ISO codes pass validation
    - **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5**

- [x] 3. Backend — integrate timezone into login and token refresh
  - [x] 3.1 Modify LoginCommandHandler to include timezone in response
    - Add `TimezoneId` (string) and `TimezoneOffsetMinutes` (int) fields to `LoginResult`
    - Call `ITimezoneResolver.Resolve(user.CountryCode, user.StateCode)` in the login handler
    - Include resolved values in the login response payload
    - _Requirements: 4.1, 4.2_

  - [x] 3.2 Modify RefreshTokenCommandHandler to recalculate timezone
    - Call `ITimezoneResolver.Resolve` during token refresh to recalculate offset (handles DST changes between sessions)
    - Include updated `TimezoneId` and `TimezoneOffsetMinutes` in refresh response
    - _Requirements: 4.4_

  - [x]* 3.3 Write unit tests for login and refresh timezone integration
    - Test login response includes timezoneId and timezoneOffsetMinutes
    - Test refresh response recalculates offset
    - Test null country defaults to Asia/Jerusalem
    - _Requirements: 4.1, 4.2, 4.4_

- [x] 4. Backend — API endpoint for user settings
  - [x] 4.1 Create UserSettingsController with location endpoints
    - Add `PUT /api/user-settings/location` endpoint calling `UpdateUserLocationCommand`
    - Add `GET /api/user-settings` endpoint calling `GetUserSettingsQuery`
    - Apply `[Authorize]` attribute to all endpoints
    - Validate tenant context via middleware
    - _Requirements: 2.1, 2.5, 6.5_

  - [x]* 4.2 Write unit tests for UserSettingsController
    - Test valid country/state update returns 200 with timezone resolution
    - Test invalid country code returns 400 with descriptive error
    - Test invalid state code for country returns 400
    - Test unauthenticated access returns 401
    - _Requirements: 2.3, 2.4, 2.5_

- [x] 5. Checkpoint — Backend complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Frontend — auth store and time formatting utility
  - [x] 6.1 Extend authStore with timezone fields
    - Add `timezoneId: string | null` and `timezoneOffsetMinutes: number` to auth state
    - Update login response handler to store `timezoneId` and `timezoneOffsetMinutes` from API response
    - Update token refresh handler to update timezone fields from refresh response
    - Default to `Asia/Jerusalem` and offset 120 if fields are missing
    - _Requirements: 4.2, 4.3, 4.5_

  - [x] 6.2 Create formatLocalTime utility
    - Create `lib/utils/formatTime.ts` with `formatLocalTime(utcIsoString, timezoneId, format)` function
    - Use `Intl.DateTimeFormat` with the IANA `timeZone` option for correct DST handling
    - Support both 24h and 12h format options
    - Ensure all outgoing API requests continue to send UTC values without client-side offset
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x]* 6.3 Write property tests for formatLocalTime
    - **Property 7: DST-Aware Time Display** — Generate random UTC datetimes and timezone IDs, verify formatted output reflects correct local time
    - **Property 8: Outgoing Requests Preserve UTC** — Verify datetime values sent to API remain in UTC
    - **Validates: Requirements 5.1, 5.3, 5.4**

- [x] 7. Frontend — replace existing time displays with formatLocalTime
  - [x] 7.1 Integrate formatLocalTime across all time-rendering components
    - Identify all components that display datetime values
    - Replace direct date formatting with calls to `formatLocalTime` using `timezoneId` from authStore
    - Ensure schedule views, assignment times, and log timestamps all use the centralized utility
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 8. Frontend — Settings page and UI restructure
  - [x] 8.1 Create Settings page route and layout
    - Create `app/settings/page.tsx` with route `/settings`
    - Add Settings link to main navigation
    - Create page layout with sections: Location, Time Format, Notification Preferences, Push Notifications
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 8.2 Implement Country/State selection UI in Location section
    - Add searchable Country dropdown showing localized country names
    - Add conditional State/Region dropdown shown only for multi-timezone countries
    - Clear State selection when Country changes
    - Display resolved timezone as read-only confirmation text after selection
    - Wire to `PUT /api/user-settings/location` on save
    - Update authStore with new timezone immediately on successful save (no re-login)
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 4.5_

  - [x] 8.3 Move existing settings sections from Profile page to Settings page
    - Move time format preference section from Profile to Settings
    - Move notification preferences section from Profile to Settings
    - Move push notification settings section from Profile to Settings
    - Ensure Profile page retains only: display name, avatar, phone, email, birthday, member since, biometric login, data export, feedback, account deletion
    - _Requirements: 6.2, 6.3, 6.4_

  - [x]* 8.4 Write unit tests for Settings page components
    - Test Country dropdown renders and is searchable
    - Test State dropdown appears/hides based on country selection
    - Test country change clears state
    - Test resolved timezone displays after selection
    - Test settings sections are present on Settings page and removed from Profile
    - _Requirements: 6.5, 7.1, 7.2, 7.3, 7.4_

- [x] 9. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- Backend uses C# (.NET), frontend uses TypeScript (Next.js)
- The step-documentation steering rule applies during implementation — each task should produce a corresponding `docs/steps/` file

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.3"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.4", "2.1", "2.2"] },
    { "id": 3, "tasks": ["2.3", "3.1", "3.2", "4.1"] },
    { "id": 4, "tasks": ["3.3", "4.2", "6.1"] },
    { "id": 5, "tasks": ["6.2"] },
    { "id": 6, "tasks": ["6.3", "7.1", "8.1"] },
    { "id": 7, "tasks": ["8.2", "8.3"] },
    { "id": 8, "tasks": ["8.4"] }
  ]
}
```
