# Implementation Plan: Space Management

## Overview

This plan implements full space lifecycle management: soft-delete/restore with cascade tracking, ownership transfer with audit trail, a four-tier permission hierarchy, space-level management timeout, space-level home-leave configuration, space name/description editing, invite code management, and the corresponding frontend settings UI. Implementation follows Clean Architecture layers (Domain → Infrastructure → Application → Api → Frontend), starting with domain entities and enums, then persistence, then commands/queries, API endpoints, and finally frontend components.

## Tasks

- [x] 1. Domain layer — Entity enhancements and new entities
  - [x] 1.1 Enhance `Space` entity with soft-delete and management timeout
    - Add `DeletedAt` (DateTime?) property with private setter
    - Add `ManagementTimeoutMinutes` (int, default 15) property with private setter
    - Implement `SoftDelete()` method: sets `DeletedAt = DateTime.UtcNow`, calls `Touch()`
    - Implement `Restore()` method: sets `DeletedAt = null`, calls `Touch()`
    - Implement `SetManagementTimeout(int minutes)` method with [5, 120] validation, throws `InvalidOperationException` if out of range
    - _Requirements: 1.1, 2.1, 5.2, 5.3, 5.4_

  - [x] 1.2 Enhance `Group` entity with cascade soft-delete tracking
    - Add `DeletedBySpaceDeletion` (bool, default false) property with private setter
    - Implement `SoftDeleteBySpace()` method: skips if already deleted, sets `DeletedAt` and `DeletedBySpaceDeletion = true`
    - Implement `RestoreFromSpaceDeletion()` method: skips if not `DeletedBySpaceDeletion`, clears both fields
    - _Requirements: 1.2, 2.2_

  - [x] 1.3 Create `SpaceHomeLeaveConfig` entity in `Jobuler.Domain/Spaces/`
    - Add all properties: SpaceId, Mode (HomeLeaveMode enum), BalanceValue, BaseDays, HomeDays, MinPeopleAtBase, MinRestHours, EligibilityThresholdHours, LeaveCapacity, LeaveDurationHours, EmergencyFreezeActive, EmergencyUseForScheduling, FreezeStartedAt, PreFreezeMode
    - Implement `ITenantScoped` interface
    - Extend `AuditableEntity`
    - Add update methods for each configurable field
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 1.4 Create `SpacePermissionLevel` enum in `Jobuler.Domain/Spaces/`
    - Define values: Member = 0, Admin = 1, GroupOwner = 2, SpaceOwner = 3
    - _Requirements: 4.1_

  - [x] 1.5 Enhance `SpaceMembership` entity with permission level
    - Add `PermissionLevel` (SpacePermissionLevel, default Member) property with private setter
    - Implement `SetPermissionLevel(SpacePermissionLevel level)` method
    - _Requirements: 4.1, 4.6_

- [x] 2. Infrastructure layer — Persistence and migrations
  - [x] 2.1 Create EF configuration for `SpaceHomeLeaveConfig` in `Jobuler.Infrastructure/Persistence/Configurations/`
    - Map to `space_home_leave_configs` table with snake_case column names
    - Configure unique index on `space_id`
    - Configure `Mode` and `PreFreezeMode` as int conversion
    - Register `DbSet<SpaceHomeLeaveConfig>` in `AppDbContext`
    - _Requirements: 6.1, 6.2_

  - [x] 2.2 Update `Space` EF configuration for new columns
    - Map `DeletedAt` to `deleted_at` (nullable timestamptz)
    - Map `ManagementTimeoutMinutes` to `management_timeout_minutes` (int, default 15)
    - _Requirements: 1.1, 5.4_

  - [x] 2.3 Update `Group` EF configuration for `DeletedBySpaceDeletion`
    - Map `DeletedBySpaceDeletion` to `deleted_by_space_deletion` (bool, default false)
    - _Requirements: 1.2, 2.2_

  - [x] 2.4 Update `SpaceMembership` EF configuration for `PermissionLevel`
    - Map `PermissionLevel` to `permission_level` (int, default 0)
    - _Requirements: 4.1_

  - [x] 2.5 Create EF migration for all schema changes
    - Generate migration via `dotnet ef migrations add AddSpaceManagement`
    - Verify migration includes: `deleted_at` and `management_timeout_minutes` on spaces, `deleted_by_space_deletion` on groups, `permission_level` on space_memberships, `space_home_leave_configs` table with RLS policy
    - _Requirements: 1.1, 1.2, 4.1, 5.4, 6.1_

- [x] 3. Infrastructure layer — PermissionService hierarchy enhancement
  - [x] 3.1 Enhance `PermissionService` to enforce four-tier hierarchy
    - If user is `Space.OwnerUserId` → all permissions granted implicitly (SpaceOwner level)
    - If user has `PermissionLevel.Admin` on SpaceMembership → management permissions granted (people, schedules, constraints)
    - If user has `PermissionLevel.GroupOwner` → group-scoped permissions granted for owned groups
    - Otherwise check explicit `SpacePermissionGrant` rows (Member level)
    - Reject requests for actions above the user's level with `UnauthorizedAccessException`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.7_

  - [x]* 3.2 Write property test for permission hierarchy enforcement (Property 4)
    - **Property 4: Permission hierarchy enforcement**
    - For any user at level L and action requiring level L' > L, service rejects; for L' ≤ L, service permits
    - **Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.7**

- [x] 4. Checkpoint — Domain and infrastructure foundation
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Application layer — Soft-delete and restore commands
  - [x] 5.1 Create `SoftDeleteSpaceCommand` and handler
    - Verify caller is Space Owner via `IPermissionService`
    - Load Space entity, call `SoftDelete()`
    - Load all Groups for the space, call `SoftDeleteBySpace()` on each
    - Log via `IAuditLogger` with action `space.soft_delete`
    - Save changes
    - _Requirements: 1.1, 1.2, 1.4, 1.5_

  - [x] 5.2 Create `RestoreSpaceCommand` and handler
    - Verify caller is Space Owner via `IPermissionService`
    - Load Space entity, reject if `DeletedAt` is null (throw `InvalidOperationException`)
    - Call `Restore()` on Space
    - Load all Groups for the space, call `RestoreFromSpaceDeletion()` on each
    - Log via `IAuditLogger` with action `space.restore`
    - Save changes
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x]* 5.3 Write property tests for soft-delete/restore (Properties 1, 2, 3)
    - **Property 1: Soft-delete/restore round trip** — For any active space, soft-delete then restore results in `DeletedAt == null`
    - **Property 2: Cascade preserves individually-deleted groups** — For N groups with M individually deleted, cascade deletes exactly (N-M) and restore restores exactly those (N-M)
    - **Property 3: Soft-deleted spaces excluded from listings** — Listing queries return only spaces with null `DeletedAt`
    - **Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2**

- [x] 6. Application layer — Ownership transfer command
  - [x] 6.1 Enhance `TransferOwnershipCommand` handler
    - Verify caller is Space Owner via `IPermissionService`
    - Validate target user is an active member of the space (throw `InvalidOperationException` if not)
    - Validate target is not the current owner (throw `InvalidOperationException` if self-transfer)
    - Update `Space.OwnerUserId` to target user
    - Grant all permission keys to new owner in `SpacePermissionGrant`
    - Create `OwnershipTransferHistory` record with previous owner, new owner, requesting user, reason, timestamp
    - Log via `IAuditLogger` with action `space.ownership_transfer`
    - Save changes
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7_

  - [x]* 6.2 Write property tests for ownership transfer (Properties 5, 6, 7)
    - **Property 5: Transfer updates owner and records history** — Transfer updates `OwnerUserId` and creates `OwnershipTransferHistory` with correct fields
    - **Property 6: Transfer grants all permissions to new owner** — After transfer, new owner has all permission keys in `SpacePermissionGrant`
    - **Property 7: Transfer rejects non-members** — Transfer to non-active-member throws `InvalidOperationException`
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.5, 3.6**

- [x] 7. Application layer — Settings commands
  - [x] 7.1 Create `UpdateManagementTimeoutCommand` and handler with FluentValidation
    - Verify caller is Space Owner via `IPermissionService`
    - Validate value is integer in [5, 120] via FluentValidation
    - Load Space entity, call `SetManagementTimeout(minutes)`
    - Save changes
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 7.2 Create `UpdateSpaceHomeLeaveConfigCommand` and handler with FluentValidation
    - Verify caller is Space Owner via `IPermissionService`
    - Validate all fields via FluentValidation (mode enum, positive integers, valid decimals)
    - Load or create `SpaceHomeLeaveConfig` for the space
    - Update all fields from command
    - Save changes
    - _Requirements: 6.1, 6.2, 6.3_

  - [x] 7.3 Create `AssignSpaceRoleCommand` and handler
    - Verify caller has `permissions.manage` via `IPermissionService`
    - Load target `SpaceMembership`, call `SetPermissionLevel(level)`
    - Log via `IAuditLogger` with action `space.role_assign`
    - Save changes
    - _Requirements: 4.6, 4.7_

  - [x] 7.4 Enhance `UpdateSpaceCommand` handler with name validation
    - Add FluentValidation: name must be 1–100 characters after trim
    - Reject empty or >100 char names with `InvalidOperationException`
    - _Requirements: 7.1, 7.2, 7.3_

  - [x] 7.5 Create `RegenerateSpaceInviteCodeCommand` and handler
    - Verify caller is Space Owner via `IPermissionService`
    - Generate new 8-character alphanumeric code
    - Update `Space.InviteCode` with new value
    - Save changes
    - _Requirements: 8.1, 8.3, 8.4_

  - [x]* 7.6 Write property tests for settings commands (Properties 8, 9, 10, 11)
    - **Property 8: Management timeout validation** — Values in [5, 120] accepted, outside rejected
    - **Property 9: Space-level timeout propagates to groups** — All groups use space-level timeout as effective value
    - **Property 10: Space name validation** — Names 1–100 chars after trim accepted, empty or >100 rejected
    - **Property 11: Invite code regeneration** — Produces 8-char alphanumeric string different from previous
    - **Validates: Requirements 5.2, 5.3, 5.4, 5.5, 7.2, 7.3, 8.3**

- [x] 8. Application layer — Queries
  - [x] 8.1 Create `GetSpaceHomeLeaveConfigQuery` and handler
    - Load `SpaceHomeLeaveConfig` for the given space
    - Return DTO with all config fields
    - _Requirements: 6.1_

  - [x] 8.2 Create `GetSpacePermissionLevelsQuery` and handler
    - Load all `SpaceMembership` records for the space with user info
    - Return list of members with their assigned permission levels
    - _Requirements: 4.6_

- [x] 9. Checkpoint — Application layer complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 10. Infrastructure — Solver payload and audit integration
  - [x] 10.1 Update solver payload normalizer to read space-level home-leave config
    - When building solver payload for a closed-base group, check for `SpaceHomeLeaveConfig` first
    - If space-level config exists, use its parameters (mode, base days, home days, min people at base, etc.) instead of group-level values
    - _Requirements: 6.3, 6.5_

  - [x] 10.2 Implement audit logging for space management actions
    - Ensure `IAuditLogger` produces entries with: actor_user_id, space_id, action, entity_type, entity_id, before/after snapshot, timestamp
    - Cover actions: soft-delete, restore, ownership transfer, role assign/revoke
    - _Requirements: 1.5, 2.5, 3.7_

  - [x]* 10.3 Write property tests for propagation and audit (Properties 12, 13)
    - **Property 12: Audit logging completeness** — Every auditable action produces an entry with actor, space ID, action name, and timestamp
    - **Property 13: Home-leave config propagates to solver payloads** — Space-level config overrides group-level values in solver payload
    - **Validates: Requirements 1.5, 2.5, 3.7, 6.2, 6.3, 6.5**

- [x] 11. API layer — Space management endpoints
  - [x] 11.1 Add soft-delete and restore endpoints to `SpacesController`
    - `DELETE /spaces/{spaceId}` → dispatches `SoftDeleteSpaceCommand`
    - `POST /spaces/{spaceId}/restore` → dispatches `RestoreSpaceCommand`
    - Both require `[Authorize]`, permission checks via handler
    - Return 204 No Content on success
    - _Requirements: 1.1, 1.4, 2.1, 2.4_

  - [x] 11.2 Add ownership transfer endpoint to `SpacesController`
    - `POST /spaces/{spaceId}/transfer-ownership` → dispatches `TransferOwnershipCommand`
    - Accept `targetUserId` and optional `reason` in body
    - Require `[Authorize]`
    - Return 204 No Content on success
    - _Requirements: 3.1, 3.4_

  - [x] 11.3 Add settings endpoints to `SpacesController`
    - `PUT /spaces/{spaceId}/management-timeout` → dispatches `UpdateManagementTimeoutCommand`
    - `PUT /spaces/{spaceId}/home-leave-config` → dispatches `UpdateSpaceHomeLeaveConfigCommand`
    - `GET /spaces/{spaceId}/home-leave-config` → dispatches `GetSpaceHomeLeaveConfigQuery`
    - `POST /spaces/{spaceId}/regenerate-invite-code` → dispatches `RegenerateSpaceInviteCodeCommand`
    - All require `[Authorize]`
    - _Requirements: 5.1, 6.1, 8.1, 8.3_

  - [x] 11.4 Add role assignment endpoint to `SpacesController`
    - `PUT /spaces/{spaceId}/members/{userId}/role` → dispatches `AssignSpaceRoleCommand`
    - `GET /spaces/{spaceId}/members/roles` → dispatches `GetSpacePermissionLevelsQuery`
    - Require `[Authorize]`
    - _Requirements: 4.6, 4.7_

  - [x] 11.5 Update listing queries to exclude soft-deleted spaces
    - Ensure all space listing endpoints filter out spaces with non-null `DeletedAt`
    - _Requirements: 1.3_

- [x] 12. Checkpoint — Backend complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 13. Frontend — API client and shared types
  - [x] 13.1 Add space management API client functions
    - Add `softDeleteSpace(spaceId)` function
    - Add `restoreSpace(spaceId)` function
    - Add `transferOwnership(spaceId, targetUserId, reason?)` function
    - Add `updateManagementTimeout(spaceId, minutes)` function
    - Add `updateHomeLeaveConfig(spaceId, config)` function
    - Add `getHomeLeaveConfig(spaceId)` function
    - Add `regenerateInviteCode(spaceId)` function
    - Add `assignSpaceRole(spaceId, userId, level)` function
    - Add `getSpacePermissionLevels(spaceId)` function
    - Define TypeScript interfaces: `SpaceHomeLeaveConfigDto`, `SpacePermissionLevelDto`, `SpacePermissionLevel` enum
    - _Requirements: 1.1, 2.1, 3.1, 5.1, 6.1, 8.3, 4.6_

- [x] 14. Frontend — ManagementTimeoutCard component
  - [x] 14.1 Create `ManagementTimeoutCard` component at `/spaces/settings`
    - Display current timeout value in minutes
    - Input field with save button
    - Validate input is integer in [5, 120] on client side
    - Call `updateManagementTimeout` API on save
    - Show success/error toast
    - Only visible to Space Owner
    - _Requirements: 5.1, 5.2, 5.3_

  - [x]* 14.2 Write unit tests for ManagementTimeoutCard
    - Test renders current value
    - Test validation rejects out-of-range values
    - Test API call dispatched with correct payload
    - Test hidden for non-owners
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 15. Frontend — HomeLeaveConfigCard component
  - [x] 15.1 Create `HomeLeaveConfigCard` component at `/spaces/settings`
    - Mode selector (Automatic, Manual, Disabled)
    - Ratio slider for balance value
    - Manual mode fields (base days, home days, min people at base)
    - Emergency freeze toggle with use-for-scheduling option
    - Call `updateHomeLeaveConfig` API on save
    - Only visible to Space Owner
    - _Requirements: 6.1, 6.2, 6.4_

  - [x]* 15.2 Write unit tests for HomeLeaveConfigCard
    - Test renders all mode options
    - Test conditional fields appear based on mode
    - Test emergency freeze toggle behavior
    - Test API call with correct payload
    - _Requirements: 6.1, 6.2_

- [x] 16. Frontend — DangerZoneCard component
  - [x] 16.1 Create `DangerZoneCard` component at `/spaces/settings`
    - Visually distinct danger zone section (red border/background)
    - Delete space button with confirmation dialog
    - Transfer ownership section with member dropdown (excludes current owner)
    - Confirmation dialog before transfer
    - Call `softDeleteSpace` and `transferOwnership` APIs respectively
    - Show success/error messages
    - Only visible to Space Owner
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x]* 16.2 Write property test for transfer target dropdown (Property 14)
    - **Property 14: Transfer target dropdown excludes current owner**
    - For N active members, dropdown shows exactly (N-1) members excluding current owner
    - **Validates: Requirements 9.4**

  - [x]* 16.3 Write unit tests for DangerZoneCard
    - Test confirmation dialog appears before delete
    - Test member dropdown excludes current owner
    - Test transfer success/error messages
    - Test hidden for non-owners
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [x] 17. Frontend — RoleAssignmentCard component
  - [x] 17.1 Create `RoleAssignmentCard` component at `/spaces/settings`
    - Display list of space members with their current permission level
    - Dropdown per member to assign new level (Member, Admin, GroupOwner)
    - Call `assignSpaceRole` API on change
    - Show success/error toast
    - Only visible to Space Owner
    - _Requirements: 4.6_

  - [x]* 17.2 Write unit tests for RoleAssignmentCard
    - Test renders all members with levels
    - Test dropdown dispatches correct API call
    - Test hidden for non-owners
    - _Requirements: 4.6_

- [x] 18. Frontend — Invite code management
  - [x] 18.1 Add invite code section to space settings page
    - Display current invite code
    - Copy to clipboard button
    - Regenerate button with confirmation
    - Call `regenerateInviteCode` API on regenerate
    - Only visible to Space Owner
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x]* 18.2 Write unit tests for invite code section
    - Test displays current code
    - Test copy button copies to clipboard
    - Test regenerate calls API and updates display
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 19. Final checkpoint — Full integration
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (FsCheck + xUnit, minimum 100 iterations)
- Unit tests validate specific examples and edge cases
- Frontend tests use Vitest + React Testing Library
- Step documentation under `docs/steps/` should be created alongside each implementation task per workspace conventions
- All commands require FluentValidation validators per architecture rules
- All endpoints require `[Authorize]` and permission checks via `IPermissionService` per security rules
- Audit log entries are append-only and include actor_user_id, space_id, action, entity_type, entity_id, before/after snapshot, timestamp
- The solver payload normalizer must prioritize space-level home-leave config over group-level values

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3", "1.4", "1.5"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3", "2.4"] },
    { "id": 2, "tasks": ["2.5", "3.1"] },
    { "id": 3, "tasks": ["3.2", "5.1", "5.2", "6.1", "7.1", "7.2", "7.3", "7.4", "7.5", "8.1", "8.2"] },
    { "id": 4, "tasks": ["5.3", "6.2", "7.6", "10.1", "10.2"] },
    { "id": 5, "tasks": ["10.3", "11.1", "11.2", "11.3", "11.4", "11.5"] },
    { "id": 6, "tasks": ["13.1"] },
    { "id": 7, "tasks": ["14.1", "15.1", "16.1", "17.1", "18.1"] },
    { "id": 8, "tasks": ["14.2", "15.2", "16.2", "16.3", "17.2", "18.2"] }
  ]
}
```
