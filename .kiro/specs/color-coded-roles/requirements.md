# Requirements Document

## Introduction

Color-coded roles allow group admins to assign a display color to each SpaceRole within a group. Members assigned to a colored role are visually distinguished in schedule views and member lists via a colored left-border/dot indicator next to their name. This provides instant visual separation by unit, station, or team (e.g., "Shahar's Unit" = yellow, "Kitchen" = red).

## Glossary

- **SpaceRole**: A dynamic operational role within a space or group (e.g., Soldier, Kitchen, Server). Stored in the `space_roles` table.
- **Role_Color**: An optional hex color string (e.g., "#f59e0b") stored on the SpaceRole entity. Maximum 7 characters.
- **Color_Palette**: A fixed set of 8-10 preset colors available for selection in the role form.
- **Color_Indicator**: A small colored left-border or dot rendered next to a person's name in schedule views and member lists.
- **Admin**: A user with the `PeopleManage` permission in the space, authorized to create and edit roles.
- **Schedule_View**: The schedule display components (ScheduleTaskTable, ScheduleTable2D) that show person names in assignment cells.
- **Member_List**: The members tab in group settings that shows member cards with role badges.

## Requirements

### Requirement 1

**User Story:** As an admin, I want to assign a color to a role when creating or editing it, so that members of that role are visually distinguishable in the schedule.

#### Acceptance Criteria

1.1. WHEN an admin creates a new group role, THE Role_Form SHALL display a Color_Palette of 8-10 preset colors for optional selection.

1.2. WHEN an admin edits an existing group role, THE Role_Form SHALL display the Color_Palette with the currently assigned Role_Color pre-selected.

1.3. WHEN an admin selects a color from the Color_Palette and saves the role, THE System SHALL persist the selected hex string as the Role_Color on the SpaceRole entity.

1.4. WHEN an admin saves a role without selecting any color, THE System SHALL store null as the Role_Color for that SpaceRole.

1.5. THE Role_Color field SHALL accept only valid 7-character hex color strings (format: "#" followed by 6 hexadecimal characters) or null.

### Requirement 2

**User Story:** As a user viewing the schedule, I want to see a colored indicator next to each person's name based on their role, so that I can quickly identify which unit or team they belong to.

#### Acceptance Criteria

2.1. WHEN a person is assigned to a role that has a Role_Color, THE Schedule_View SHALL render a Color_Indicator (left border or dot) in that color next to the person's name.

2.2. WHEN a person is assigned to a role that has no Role_Color (null), THE Schedule_View SHALL render the person's name without any Color_Indicator, using the default slate/gray styling.

2.3. WHEN a person has no role assignment, THE Schedule_View SHALL render the person's name without any Color_Indicator.

2.4. THE Color_Indicator SHALL appear consistently in both the ScheduleTaskTable and ScheduleTable2D components.

### Requirement 3

**User Story:** As a user viewing the members list, I want to see the role color indicator on member cards, so that I can identify team membership at a glance.

#### Acceptance Criteria

3.1. WHEN a member has a role with a Role_Color, THE Member_List SHALL display a Color_Indicator next to or on the member's role badge.

3.2. WHEN a member has a role without a Role_Color, THE Member_List SHALL display the role badge in the default styling without a Color_Indicator.

### Requirement 4

**User Story:** As a developer, I want the backend to store and serve the role color, so that the frontend can render color indicators.

#### Acceptance Criteria

4.1. THE SpaceRole entity SHALL include an optional Color property of type string, nullable, with a maximum length of 7 characters.

4.2. WHEN the API returns role data (via GET endpoints), THE response DTO SHALL include the color field.

4.3. WHEN the API receives a create or update role request with a color value, THE System SHALL validate that the color matches the 7-character hex format or is null.

4.4. IF the API receives a create or update role request with an invalid color value, THEN THE System SHALL reject the request with a 400 Bad Request response.

4.5. THE database migration SHALL add a nullable `color` column of type TEXT (max 7 chars) to the `space_roles` table.
