# Requirements Document

## Introduction

This feature changes the onboarding flow so that new users create or join a Space (organization) before creating or joining groups within it. Currently, the system has spaces and groups already linked via `space_id`, but the onboarding flow skips the space creation step and sends users directly to group creation. This feature makes Space the primary organizational unit in the user journey, adds group linking (parent-child hierarchy), supports multi-space membership, and provides a migration path for existing users.

## Glossary

- **Space**: An organizational unit representing a real-world entity (e.g., an army platoon, a sub-base, an emergency post). Each Space has an owner, members, and contains groups.
- **Group**: A scheduling unit within a Space. Groups contain members, tasks, constraints, and schedules.
- **Onboarding_Wizard**: A multi-step guided flow presented to new users after registration that walks them through Space creation and initial group setup.
- **Space_Switcher**: A UI component that allows users to switch between Spaces they belong to.
- **Parent_Group**: A group that has one or more child groups linked to it for cascading schedule propagation.
- **Child_Group**: A group linked to a parent group that inherits or cascades scheduling constraints from the parent.
- **Space_Invite**: An invitation mechanism (link or code) that allows existing Space members to invite new users to join their Space.
- **Migration_Service**: A backend service that assigns existing orphaned groups to their owner's Space during the transition period.
- **Setup_Guide**: A contextual checklist shown to users who have not completed the essential onboarding steps within a Space.

## Requirements

### Requirement 1: Space Creation During Onboarding

**User Story:** As a new user, I want to create a Space immediately after registration, so that I have an organizational context before creating groups.

#### Acceptance Criteria

1. WHEN a new user completes registration and has zero Space memberships, THE Onboarding_Wizard SHALL present a Space creation step as the first action.
2. WHEN the user submits a Space name (2–100 characters, inclusive), THE Onboarding_Wizard SHALL create the Space and assign the user as owner.
3. WHEN the Space is created, THE Onboarding_Wizard SHALL set the Space locale to the user's current interface language (he, en, or ru).
4. WHEN the Space is created successfully, THE Onboarding_Wizard SHALL navigate the user to the group creation step within that Space within 2 seconds of receiving the server confirmation.
5. IF the Space name is empty, fewer than 2 characters, or exceeds 100 characters, THEN THE Onboarding_Wizard SHALL display a validation error indicating the name must be between 2 and 100 characters, and SHALL prevent submission.
6. IF the Space creation request fails due to a server error or network failure, THEN THE Onboarding_Wizard SHALL display an error message indicating the Space could not be created, SHALL retain the user's entered Space name in the input field, and SHALL allow the user to retry submission.
7. WHILE the Space creation request is in progress, THE Onboarding_Wizard SHALL disable the submit button and display a loading indicator.

### Requirement 2: Join Existing Space During Onboarding

**User Story:** As a new user who was invited to an existing Space, I want to join that Space during onboarding, so that I can skip creating my own Space.

#### Acceptance Criteria

1. WHEN a new user has zero Space memberships, THE Onboarding_Wizard SHALL offer an option to join an existing Space via invite code.
2. WHEN the user enters a valid 8-character alphanumeric Space invite code, THE Onboarding_Wizard SHALL add the user as a member of that Space and grant the `space.view` permission.
3. WHEN the user successfully joins a Space, THE Onboarding_Wizard SHALL navigate the user to the home page with that Space selected as active.
4. IF the invite code is invalid or has been regenerated (invalidated), THEN THE Onboarding_Wizard SHALL display an error message indicating the code is not recognized and allow the user to retry or create a new Space instead.
5. IF the user submits an incorrectly formatted invite code (not exactly 8 alphanumeric characters), THEN THE Onboarding_Wizard SHALL display a validation error and prevent submission.
6. IF the user submits invalid invite codes 5 times consecutively within a single onboarding session, THEN THE Onboarding_Wizard SHALL disable the invite code input for 60 seconds before allowing further attempts.
7. IF the user submits a valid invite code for a Space they already belong to, THEN THE Onboarding_Wizard SHALL navigate the user to the home page with that Space selected as active without creating a duplicate membership.

### Requirement 3: Space Invite Generation

**User Story:** As a Space owner, I want to generate invite codes for my Space, so that I can invite new members to join without manually adding them.

#### Acceptance Criteria

1. WHEN a Space is created, THE Space_Invite system SHALL generate a unique 8-character uppercase alphanumeric code (characters A-Z, 0-9) for that Space, where uniqueness is enforced across all active Space invite codes in the system.
2. WHEN a Space owner requests a new invite code, THE Space_Invite system SHALL replace the previous code with a newly generated code, rendering the previous code permanently unusable for joining.
3. WHEN a user submits a valid Space invite code, THE Space_Invite system SHALL create a SpaceMembership record linking the user to the Space.
4. WHEN a user submits a valid Space invite code, THE Space_Invite system SHALL grant the `space.view` permission to the joining user.
5. IF a user submits a Space invite code for a Space they already belong to, THEN THE Space_Invite system SHALL return a response indicating the user is already a member, without creating a duplicate SpaceMembership record or duplicate permission grant.
6. IF a user submits a code that does not match any active Space invite code, THEN THE Space_Invite system SHALL reject the request with an error message indicating the code is invalid.

### Requirement 4: Space Management

**User Story:** As a Space owner, I want to manage my Space settings (rename, update description, view members), so that I can keep the organization information current.

#### Acceptance Criteria

1. WHEN a Space owner submits an updated name (2–100 characters, not blank), description (0–500 characters), and locale, THE Space system SHALL persist the trimmed values and confirm the update.
2. IF a Space owner submits a name shorter than 2 characters, longer than 100 characters, or consisting only of whitespace, THEN THE Space system SHALL reject the request with an error message indicating the name constraint violated.
3. WHEN a Space owner views the Space settings page, THE Space system SHALL display the current name, description, locale, active member count, and group count.
4. WHEN a Space owner requests the member list, THE Space system SHALL return all SpaceMembership records where IsActive is true for that Space, including each member's user identifier and joined date.
5. IF a non-owner user attempts to update Space settings, THEN THE Space system SHALL reject the request with a 403 status.
6. IF a Space owner attempts to update a Space that does not exist, THEN THE Space system SHALL return a 404 status.

### Requirement 5: Group-to-Space Relationship Enforcement

**User Story:** As a user, I want all groups to belong to a Space, so that organizational hierarchy is clear and tenant isolation is maintained.

#### Acceptance Criteria

1. THE Group system SHALL require a non-null `space_id` that references an existing, active Space for every group creation request.
2. WHEN a user creates a group, THE Group system SHALL verify that the user holds the required permission in the target Space (via `IPermissionService`) before allowing creation.
3. WHEN a user lists groups for a given Space, THE Group system SHALL return only non-deleted groups belonging to the Space identified by the `spaceId` route parameter.
4. IF a group creation request references a Space the user does not have permission to access, THEN THE Group system SHALL reject the request with an authorization error and not persist any data.
5. IF a group creation request provides a `space_id` that does not match any existing active Space, THEN THE Group system SHALL reject the request with a not-found error.

### Requirement 6: Linked Groups (Parent-Child Hierarchy)

**User Story:** As a Space admin, I want to link groups in a parent-child relationship, so that schedules can cascade from parent to child groups.

#### Acceptance Criteria

1. WHEN a Space admin sets a parent group for a child group, THE Group system SHALL store the `parent_group_id` on the child group record.
2. IF a user attempts to set a parent group that belongs to a different Space than the child group, THEN THE Group system SHALL reject the request with a validation error indicating both groups must belong to the same Space.
3. IF a user attempts to set a parent group that is itself a child of another group, THEN THE Group system SHALL reject the request with a validation error indicating that only single-level hierarchy is allowed.
4. IF a user attempts to assign a child to a group that already has a parent (making it both a child and a parent), THEN THE Group system SHALL reject the request with a validation error indicating that a child group cannot be a parent.
5. WHEN a parent group is soft-deleted, THE Group system SHALL unlink all child groups by setting their `parent_group_id` to null.
6. WHEN a Space admin removes the parent link from a child group, THE Group system SHALL set the child group's `parent_group_id` to null.
7. WHEN a parent group's schedule version is published, THE Group system SHALL include the parent group's published assignment data in the solver input payload of each child group's next schedule run for cascading constraint evaluation.

### Requirement 7: Space Switching

**User Story:** As a user who belongs to multiple Spaces, I want to switch between them, so that I can manage different organizations from a single account.

#### Acceptance Criteria

1. IF the user belongs to multiple Spaces, THEN THE Space_Switcher SHALL display a list of all Spaces the user is a member of, showing each Space's name.
2. IF the user belongs to exactly one Space, THEN THE Space_Switcher SHALL automatically select that Space and navigate to the groups view without showing the Space selection list.
3. WHEN the user selects a different Space from the list, THE Space_Switcher SHALL update the active Space context, invalidate all Space-scoped cached data (groups, schedules, members), and refetch the data for the newly selected Space within 3 seconds.
4. WHEN the user switches Spaces, THE Space_Switcher SHALL persist the selected Space identifier in local storage so it remains active across page reloads.
5. THE Space_Switcher SHALL display the currently active Space name in the sidebar navigation, truncated with an ellipsis if it exceeds 30 characters.
6. WHILE a Space is selected, THE Space_Switcher SHALL include that Space's `space_id` in all Space-scoped API requests.
7. IF the persisted Space identifier is no longer valid (user was removed from that Space or the Space was deleted), THEN THE Space_Switcher SHALL clear the invalid selection and redirect the user to the Space selection list.
8. IF the Space list API request fails, THEN THE Space_Switcher SHALL display an error message indicating the Spaces could not be loaded and provide a retry option.

### Requirement 8: Profile Settings Shared Across Spaces

**User Story:** As a user in multiple Spaces, I want my profile settings (display name, language, notification preferences) to be shared across all Spaces, so that I do not need to configure them separately.

#### Acceptance Criteria

1. THE User_Settings system SHALL store profile settings (display name, locale preference, profile image URL) at the user level, not the Space level.
2. WHEN a user updates their display name in any Space context, THE User_Settings system SHALL persist the new display name (between 1 and 100 characters, trimmed of leading/trailing whitespace) and reflect the change across all Spaces the user belongs to within 2 seconds.
3. WHEN a user changes their locale preference, THE User_Settings system SHALL apply the new locale to the entire application regardless of active Space, using a supported locale code from the system's configured locale list.
4. IF a user submits a display name that is empty or exceeds 100 characters after trimming, THEN THE User_Settings system SHALL reject the update and return a validation error indicating the display name length constraint.
5. IF a user submits an unsupported locale code, THEN THE User_Settings system SHALL reject the update and return a validation error indicating the locale is not recognized.

### Requirement 9: Migration for Existing Users

**User Story:** As an existing user with groups that were created before the space-first flow, I want my groups to be automatically associated with a Space, so that I can continue using the system without disruption.

#### Acceptance Criteria

1. WHEN an existing user logs in and has groups (where `CreatedByUserId` matches the user) but no SpaceMembership record, THE Migration_Service SHALL create a default Space named "{user's display name}'s Space", truncated to 100 characters, with the locale set to the user's preferred locale.
2. WHEN the Migration_Service creates a default Space, THE Migration_Service SHALL update the `space_id` of all groups where `CreatedByUserId` matches the user and `space_id` is unset or references a non-existent Space, assigning them to the newly created Space.
3. WHEN the Migration_Service creates a default Space, THE Migration_Service SHALL set the user as the Space owner and create a SpaceMembership record linking the user to that Space.
4. THE Migration_Service SHALL execute the migration exactly once per user, record the migration timestamp, and skip execution on subsequent logins if a migration record already exists for that user.
5. WHEN the migration completes successfully, THE Migration_Service SHALL redirect the user to the home page with the new Space selected as active.
6. IF the migration fails at any step (Space creation, group reassignment, or membership creation), THEN THE Migration_Service SHALL roll back all changes from that migration attempt, allow the user to proceed to the home page, and log the failure for operational review.
7. IF the user has groups but the user's display name is empty or whitespace, THEN THE Migration_Service SHALL use "My Space" as the default Space name.

### Requirement 10: Setup Guide for New Space Members

**User Story:** As a new Space member, I want to see a setup guide showing me what steps to complete, so that I can get started with the system efficiently.

#### Acceptance Criteria

1. WHEN a user navigates to a Space's home page and has no prior Setup_Guide state for that Space, THE Setup_Guide SHALL display a checklist of onboarding steps (create/join a group, add members, define tasks, run solver).
2. WHEN the system detects that the user has performed the action corresponding to a step (created or joined a group, added at least one member, defined at least one task, executed the solver at least once), THE Setup_Guide SHALL automatically mark that step as complete and persist the completion state per user per Space.
3. WHEN all four steps are marked complete, THE Setup_Guide SHALL transition to a "completed" state and stop displaying automatically on subsequent visits.
4. THE Setup_Guide SHALL provide a "dismiss" action that persists a hidden state for that user and Space, preventing the guide from displaying on subsequent visits until the user triggers a restart.
5. WHEN the user activates the "restart" action from the home page, THE Setup_Guide SHALL clear all step completion states and the dismissed state for that user and Space, and re-display the guide with all steps unmarked.

### Requirement 11: Space-Level Permissions

**User Story:** As a Space owner, I want to control what members can do at the Space level, so that I can delegate management tasks while maintaining security.

#### Acceptance Criteria

1. WHEN the Permission system evaluates whether a user holds a given permission within a Space, THE Permission system SHALL check for an active (non-revoked) `SpacePermissionGrant` record matching the user, Space, and permission key.
2. WHEN a user with `permissions.manage` grants a permission to a member, THE Permission system SHALL create a `SpacePermissionGrant` record with the granting user's ID, target user's ID, permission key, and the current UTC timestamp as `GrantedAt`.
3. WHEN a user with `permissions.manage` revokes a permission, THE Permission system SHALL set the `RevokedAt` timestamp to the current UTC time on the matching grant record without deleting it.
4. THE Permission system SHALL treat the Space owner as implicitly holding all permissions without requiring explicit grant records.
5. IF a user without `permissions.manage` attempts to grant or revoke permissions, THEN THE Permission system SHALL reject the request with a 403 status.
6. IF a grant request specifies a permission key that is not in the defined set of known permission keys, THEN THE Permission system SHALL reject the request with an error indicating the permission key is invalid.
7. IF a grant request targets a user who already holds an active (non-revoked) grant for the same permission key in the same Space, THEN THE Permission system SHALL reject the request with an error indicating the permission is already granted.
8. WHEN a permission is granted or revoked, THE Permission system SHALL produce an audit log entry recording the actor user ID, Space ID, action (grant or revoke), target user ID, permission key, and timestamp.

### Requirement 12: Onboarding Flow Redirect Logic

**User Story:** As a returning user, I want to be redirected to the appropriate page based on my Space membership status, so that I do not see the onboarding wizard unnecessarily.

#### Acceptance Criteria

1. WHEN an authenticated user navigates to the application root and has at least one Space membership, THE Routing system SHALL redirect to the home page with the last-used Space (as persisted in local storage) active.
2. WHEN an authenticated user navigates to the application root and has zero Space memberships, THE Routing system SHALL redirect to the Onboarding_Wizard.
3. WHEN the Onboarding_Wizard is completed (Space created or joined), THE Routing system SHALL redirect to the home page with the newly created or joined Space set as the active Space.
4. IF the membership check on app load determines the user has been removed from all Spaces, THEN THE Routing system SHALL clear the stored last-used Space and redirect the user to the Onboarding_Wizard.
5. IF the user has Space memberships but the last-used Space stored in local storage is no longer valid (user removed from that Space), THEN THE Routing system SHALL fall back to the most recently joined Space and update local storage accordingly.
6. IF an authenticated user with at least one Space membership attempts to navigate directly to the Onboarding_Wizard URL, THEN THE Routing system SHALL redirect the user to the home page instead.
