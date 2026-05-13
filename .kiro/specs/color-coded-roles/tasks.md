# Implementation Plan: Color-Coded Roles

## Overview

Add an optional color property to SpaceRole, expose it through the API, and render a colored left-border/dot indicator next to person names in schedule views and member lists. Implementation flows from backend (domain → infrastructure → application → API) to frontend (API client → color picker → schedule views → member list).

## Tasks

- [x] 1. Backend: Add Color property to SpaceRole entity and database
  - [x] 1.1 Add optional `Color` property to `SpaceRole` domain entity
    - Add `public string? Color { get; private set; }` to `SpaceRole.cs`
    - Update `CreateForGroup` factory method to accept optional `color` parameter
    - Update `Create` factory method to accept optional `color` parameter
    - Update `Update` method to accept and set `color` parameter
    - _Requirements: 4.1_
  - [x] 1.2 Add database migration `044_space_role_color.sql`
    - Add nullable `color` TEXT column to `space_roles` table
    - Add CHECK constraint: `color IS NULL OR color ~ '^#[0-9a-fA-F]{6}$'`
    - _Requirements: 4.5_
  - [x] 1.3 Update EF Core `SpaceRoleConfiguration` to map the Color property
    - Add `builder.Property(r => r.Color).HasColumnName("color").HasMaxLength(7).IsRequired(false);`
    - _Requirements: 4.1, 4.5_

- [x] 2. Backend: Update commands, validation, and API layer
  - [x] 2.1 Update `CreateGroupRoleCommand` and its handler to include `Color`
    - Add `string? Color` parameter to the command record
    - Pass color to `SpaceRole.CreateForGroup` in the handler
    - _Requirements: 1.3, 4.3_
  - [x] 2.2 Update `UpdateGroupRoleCommand` and its handler to include `Color`
    - Add `string? Color` parameter to the command record
    - Pass color to `SpaceRole.Update` in the handler
    - _Requirements: 1.3, 4.3_
  - [x] 2.3 Add FluentValidation rule for the Color field
    - Validate color matches `^#[0-9a-fA-F]{6}$` when not null
    - Return 400 Bad Request for invalid values
    - _Requirements: 1.5, 4.3, 4.4_
  - [x] 2.4 Update `GroupRolesController` and request/response DTOs
    - Add `Color` to `GroupRoleRequest` record
    - Pass color from request to commands in Create and Update actions
    - Ensure `GetGroupRolesQuery` response includes the color field
    - _Requirements: 4.2, 4.3_
  - [ ]* 2.5 Write property test for hex color validation
    - **Property 1: Hex color validation correctness**
    - Generate random strings and verify the validator accepts only valid hex colors or null
    - **Validates: Requirements 1.5, 4.3, 4.4**

- [x] 3. Checkpoint - Backend verification
  - Ensure all tests pass, ask the user if questions arise.
  - Run the migration against a local database to verify the column is added correctly.

- [x] 4. Frontend: Update API client and add color picker
  - [x] 4.1 Update `GroupRoleDto` interface and API functions
    - Add `color: string | null` to `GroupRoleDto` in `lib/api/groups.ts`
    - Update `createGroupRole` and `updateGroupRole` payload types to include `color`
    - _Requirements: 4.2_
  - [x] 4.2 Create `RoleColorPicker` component
    - Create a new component in `components/` with 8-10 preset color circles
    - Support `value` (selected color or null) and `onChange` callback props
    - Show a ring/check on the selected color; clicking selected deselects (null)
    - _Requirements: 1.1, 1.2_
  - [x] 4.3 Integrate color picker into the RolesTab form
    - Add `RoleColorPicker` to the create-role form (below permission level select)
    - Add `RoleColorPicker` to the edit-role inline form, pre-selected with current color
    - Pass color value in `onCreateRole` and `onUpdateRole` callbacks
    - Update `handleCreateRole` and `handleUpdateRole` in the group page to pass color to API
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 5. Frontend: Render color indicators in schedule views and member list
  - [x] 5.1 Build a role-color lookup utility
    - Create a helper that builds a `Map<personId, roleColor>` from members + groupRoles
    - Pass this map (or a `getRoleColor(personId)` function) as a prop to schedule components
    - _Requirements: 2.1, 2.2, 2.3_
  - [x] 5.2 Add color indicator to `ScheduleTaskTable`
    - Accept a `roleColorMap` prop (or similar)
    - Render a 3px left border in the role color on the person name wrapper when color exists
    - No indicator when color is null or person has no role
    - _Requirements: 2.1, 2.2, 2.3, 2.4_
  - [x] 5.3 Add color indicator to `ScheduleTable2D`
    - Same approach as ScheduleTaskTable — left border on person name div
    - _Requirements: 2.1, 2.2, 2.3, 2.4_
  - [x] 5.4 Add color indicator to member list in RolesTab
    - Show a small colored dot before the role badge on member cards
    - No dot when role has no color
    - _Requirements: 3.1, 3.2_
  - [ ]* 5.5 Write property test for color indicator rendering
    - **Property 3: Color indicator rendering consistency**
    - For any person with a non-null role color, verify the rendered output contains the color indicator styled with that color. For null/no-role, verify no indicator.
    - **Validates: Requirements 2.1, 2.2, 2.3, 3.1, 3.2**

- [x] 6. Final checkpoint - Full integration verification
  - Ensure all tests pass, ask the user if questions arise.
  - Verify the color picker appears in role create/edit forms.
  - Verify color indicators render in both schedule table views and member list.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- The preset palette approach avoids accessibility issues with arbitrary colors — all presets are chosen for good contrast
- The `personId` is already available in `TaskAssignment` data, so no additional API calls are needed for schedule views
- Migration uses a CHECK constraint as a database-level safety net in addition to application-level validation
