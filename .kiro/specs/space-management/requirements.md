# Requirements Document

## Introduction

This feature adds space-level management capabilities that mirror and consolidate existing group-level settings. It introduces soft-delete for spaces (with archival and restoration), space ownership transfer, a formal permission hierarchy (Space Owner, Group Owner, Admin, Member), and moves settings that logically belong at the space level out of the group settings. The goal is to give space owners full lifecycle control over their space and establish a clear, centralized authority model.

## Glossary

- **Space**: The top-level tenant entity in the multi-tenant architecture. A Space contains multiple Groups and is owned by a single user.
- **Space_Owner**: The user identified by `Space.OwnerUserId` who has full administrative control over the Space and all its Groups. Highest permission level.
- **Group_Owner**: A user who owns a specific Group within the Space. Has full control over that Group but not over other Groups or space-level settings.
- **Admin**: A user with elevated permissions within the Space (can manage people, schedules, and constraints) but cannot perform destructive space-level actions like deletion or ownership transfer.
- **Member**: A regular user within the Space with read access and the ability to submit availability and preferences.
- **Space_Settings_Page**: The frontend page at `/spaces/settings` where the Space_Owner manages space configuration.
- **Space_Management_Service**: The backend application-layer service responsible for processing space management commands (delete, restore, transfer, settings updates).
- **IPermissionService**: The authorization service that verifies whether a user holds a specific permission within a space before allowing privileged operations.
- **Soft_Delete**: A reversible deletion pattern where the entity is marked with a `DeletedAt` timestamp rather than being physically removed from the database.
- **Management_Timeout**: A configurable duration (in minutes) after which an admin session in management mode automatically expires, requiring re-authentication.
- **Invite_Code**: A short alphanumeric code that allows users to join a Space without a direct invitation.
- **Home_Leave_Config**: Configuration parameters that control how the solver schedules leave rotations for closed-base groups (rest hours, eligibility, capacity, duration, mode).
- **Permission_Level**: One of four hierarchical roles (Space_Owner > Group_Owner > Admin > Member) that determines what actions a user can perform.

## Requirements

### Requirement 1: Soft-Delete Space

**User Story:** As a Space_Owner, I want to soft-delete my entire space, so that I can archive it and all its data with the possibility of restoring it later.

#### Acceptance Criteria

1. WHEN the Space_Owner requests deletion of a Space, THE Space_Management_Service SHALL set the `DeletedAt` timestamp on the Space entity to the current UTC time
2. WHEN a Space is soft-deleted, THE Space_Management_Service SHALL soft-delete all Groups belonging to that Space by setting their `DeletedAt` timestamps
3. WHILE a Space has a non-null `DeletedAt` timestamp, THE Space_Management_Service SHALL exclude that Space from all listing queries for its members
4. IF a non-owner user requests deletion of a Space, THEN THE Space_Management_Service SHALL reject the request with an unauthorized error
5. WHEN a Space is soft-deleted, THE Space_Management_Service SHALL produce an audit log entry recording the actor, space ID, and timestamp

### Requirement 2: Restore Soft-Deleted Space

**User Story:** As a Space_Owner, I want to restore a previously soft-deleted space, so that I can recover my data if the deletion was accidental or no longer desired.

#### Acceptance Criteria

1. WHEN the Space_Owner requests restoration of a soft-deleted Space, THE Space_Management_Service SHALL set the `DeletedAt` timestamp to null on the Space entity
2. WHEN a Space is restored, THE Space_Management_Service SHALL restore all Groups that were soft-deleted as part of the space deletion (Groups that were individually deleted before the space deletion remain deleted)
3. IF a user requests restoration of a Space that is not soft-deleted, THEN THE Space_Management_Service SHALL reject the request with an invalid operation error
4. IF a non-owner user requests restoration of a Space, THEN THE Space_Management_Service SHALL reject the request with an unauthorized error
5. WHEN a Space is restored, THE Space_Management_Service SHALL produce an audit log entry recording the actor, space ID, and timestamp

### Requirement 3: Transfer Space Ownership

**User Story:** As a Space_Owner, I want to transfer ownership of my space to another member, so that I can hand off administrative responsibility when needed.

#### Acceptance Criteria

1. WHEN the Space_Owner initiates an ownership transfer specifying a target user, THE Space_Management_Service SHALL update `Space.OwnerUserId` to the target user's ID
2. THE Space_Management_Service SHALL verify that the target user is an existing active member of the Space before completing the transfer
3. IF the target user is not an active member of the Space, THEN THE Space_Management_Service SHALL reject the transfer with an invalid operation error
4. IF a non-owner user requests an ownership transfer, THEN THE Space_Management_Service SHALL reject the request with an unauthorized error
5. WHEN an ownership transfer completes, THE Space_Management_Service SHALL record the transfer in the `OwnershipTransferHistory` table with previous owner, new owner, requesting user, and optional reason
6. WHEN an ownership transfer completes, THE Space_Management_Service SHALL grant all space permissions to the new owner
7. WHEN an ownership transfer completes, THE Space_Management_Service SHALL produce an audit log entry recording the actor, space ID, previous owner, and new owner

### Requirement 4: Space Permission Hierarchy

**User Story:** As a Space_Owner, I want a clear permission hierarchy (Space Owner, Group Owner, Admin, Member), so that I can delegate responsibilities with appropriate access levels.

#### Acceptance Criteria

1. THE Space_Management_Service SHALL enforce four permission levels in descending order of authority: Space_Owner, Group_Owner, Admin, Member
2. WHILE a user has the Space_Owner permission level, THE IPermissionService SHALL grant that user all permissions across the Space and all its Groups
3. WHILE a user has the Group_Owner permission level, THE IPermissionService SHALL grant that user full control over their owned Groups but restrict space-level destructive actions (delete space, transfer space ownership)
4. WHILE a user has the Admin permission level, THE IPermissionService SHALL grant that user people management, schedule management, and constraint management permissions but restrict ownership transfer and space deletion
5. WHILE a user has the Member permission level, THE IPermissionService SHALL grant that user read access to their assigned Groups and the ability to submit availability and preferences
6. THE Space_Settings_Page SHALL display a role assignment interface allowing the Space_Owner to assign permission levels to space members
7. IF a user attempts an action above their permission level, THEN THE IPermissionService SHALL reject the request with an unauthorized error

### Requirement 5: Space-Level Management Timeout

**User Story:** As a Space_Owner, I want to configure a management timeout at the space level that applies to all groups, so that admin sessions expire consistently across my space.

#### Acceptance Criteria

1. THE Space_Settings_Page SHALL display a management timeout input field with the current value in minutes
2. WHEN the Space_Owner submits a new timeout value, THE Space_Management_Service SHALL validate that the value is an integer between 5 and 120 minutes
3. IF the submitted timeout value is outside the valid range, THEN THE Space_Management_Service SHALL reject the request with a validation error
4. WHEN the management timeout is updated, THE Space_Management_Service SHALL persist the new value on the Space entity
5. THE Space_Management_Service SHALL use the space-level management timeout as the authoritative timeout for all Groups within the Space (replacing the group-level setting)
6. WHEN the space-level management timeout is set, THE Space_Management_Service SHALL remove the management timeout setting from the group-level settings UI

### Requirement 6: Space-Level Home Leave Configuration

**User Story:** As a Space_Owner, I want to configure home-leave settings at the space level, so that leave policies are managed centrally rather than per-group.

#### Acceptance Criteria

1. THE Space_Settings_Page SHALL display the home-leave configuration panel (mode selector, ratio slider, manual mode, emergency freeze, min people at base) for the Space_Owner
2. WHEN the Space_Owner updates home-leave configuration, THE Space_Management_Service SHALL persist the configuration at the space level
3. THE Space_Management_Service SHALL apply the space-level home-leave configuration to all closed-base Groups within the Space
4. WHEN the home-leave configuration is moved to the space level, THE Space_Management_Service SHALL remove the home-leave configuration panel from the group-level settings UI
5. WHILE a Space has home-leave configuration set, THE Space_Management_Service SHALL use those parameters when generating solver payloads for any closed-base Group in the Space

### Requirement 7: Edit Space Name and Description

**User Story:** As a Space_Owner, I want to edit my space name and description from the settings page, so that I can keep the space information current.

#### Acceptance Criteria

1. WHEN the Space_Owner submits a new name for the Space, THE Space_Settings_Page SHALL send the update request to the backend
2. THE Space_Management_Service SHALL validate that the new name is between 1 and 100 characters after trimming whitespace
3. IF the submitted name is empty or exceeds 100 characters, THEN THE Space_Management_Service SHALL reject the request with a validation error
4. WHEN the name is successfully updated, THE Space_Settings_Page SHALL reflect the new name in the sidebar and page header without requiring a full page reload

### Requirement 8: Space Invite Code Management

**User Story:** As a Space_Owner, I want to manage the space invite code from the settings page, so that I can control who can join my space.

#### Acceptance Criteria

1. THE Space_Settings_Page SHALL display the current invite code to the Space_Owner
2. WHEN the Space_Owner clicks the copy button, THE Space_Settings_Page SHALL copy the invite code to the clipboard
3. WHEN the Space_Owner requests code regeneration, THE Space_Management_Service SHALL generate a new 8-character alphanumeric invite code and invalidate the previous code
4. IF a non-owner user attempts to view or regenerate the invite code, THEN THE Space_Management_Service SHALL reject the request with an unauthorized error

### Requirement 9: Space Settings UI — Danger Zone

**User Story:** As a Space_Owner, I want the space settings page to include a danger zone section, so that destructive actions (delete space, transfer ownership) are clearly separated and require confirmation.

#### Acceptance Criteria

1. THE Space_Settings_Page SHALL display a visually distinct "Danger Zone" section containing the delete space and transfer ownership controls
2. WHEN the Space_Owner clicks the delete space button, THE Space_Settings_Page SHALL display a confirmation dialog before sending the delete request
3. WHEN the Space_Owner selects a transfer target and confirms, THE Space_Settings_Page SHALL send the transfer request and display a success or error message
4. THE Space_Settings_Page SHALL display a dropdown of current space members as transfer targets, excluding the current owner
5. WHILE a Space is in a soft-deleted state, THE Space_Settings_Page SHALL not be accessible (the space is hidden from listings)
