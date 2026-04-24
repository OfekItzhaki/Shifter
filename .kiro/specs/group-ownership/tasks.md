# Implementation Plan: Group Ownership

## Overview

Introduces a formal ownership model for groups: creator auto-membership, owner-only management actions (rename, soft-delete with 30-day recovery, ownership transfer via email confirmation), owner removal protection, group avatars, and a 5th seed user (Dana). Spans migration 009, domain entity changes, new Application-layer commands/queries, controller endpoints, and frontend UI additions.

## Tasks

- [x] 1. Migration 009 — group ownership schema
  - Create `infra/migrations/009_group_ownership.sql`
  - Add `is_owner BOOLEAN NOT NULL DEFAULT false` to `group_memberships`
  - Add unique partial index `uq_group_memberships_one_owner ON group_memberships (group_id) WHERE is_owner = true`
  - Add `deleted_at TIMESTAMPTZ` nullable column to `groups`
  - Create `pending_ownership_transfers` table with all columns and indexes as specified in the design
  - _Requirements: 2.2, 2.5, 6.5, 8.3_

- [x] 2. Domain entity changes
  - [x] 2.1 Update `GroupMembership` entity
    - Add `IsOwner` property with private setter
    - Add optional `isOwner` parameter to `GroupMembership.Create()` (default `false`)
    - Add `SetOwner(bool isOwner)` method
    - File: `apps/api/Jobuler.Domain/Groups/GroupMembership.cs`
    - _Requirements: 2.1, 2.2_

  - [x] 2.2 Update `Group` entity
    - Add `DeletedAt DateTime?` property with private setter
    - Add `SoftDelete()` method — sets `DeletedAt = DateTime.UtcNow`
    - Add `Restore()` method — sets `DeletedAt = null`
    - Add `Rename(string name)` method — trims, validates 1–100 chars, throws `InvalidOperationException` if invalid
    - File: `apps/api/Jobuler.Domain/Groups/Group.cs`
    - _Requirements: 4.1, 6.2, 7.3_

  - [x] 2.3 Create `PendingOwnershipTransfer` domain entity
    - New file: `apps/api/Jobuler.Domain/Groups/PendingOwnershipTransfer.cs`
    - Properties: `SpaceId`, `GroupId`, `CurrentOwnerPersonId`, `ProposedOwnerPersonId`, `ConfirmationToken` (random 64-char hex), `CreatedAt`, `ExpiresAt` (CreatedAt + 48h)
    - Static factory `Create(...)` method
    - `IsExpired` computed property: `DateTime.UtcNow > ExpiresAt`
    - Implement `ITenantScoped`
    - _Requirements: 8.3, 9.1_

- [x] 3. `IEmailSender` interface and `NoOpEmailSender`
  - Create `apps/api/Jobuler.Application/Common/IEmailSender.cs` with `SendAsync(to, subject, htmlBody, ct)` method
  - Create `apps/api/Jobuler.Infrastructure/Email/NoOpEmailSender.cs` implementing `IEmailSender` — logs at Debug level, returns `Task.CompletedTask`
  - Register `services.AddScoped<IEmailSender, NoOpEmailSender>()` in Infrastructure DI setup
  - _Requirements: 7a.1, 7a.2, 7a.3_

- [x] 4. Fix `CreateGroupCommand` — auto-membership and owner assignment
  - Add `CreatedByUserId Guid` parameter to `CreateGroupCommand` record
  - In handler: resolve `Person` where `LinkedUserId == CreatedByUserId && SpaceId == req.SpaceId`; throw `KeyNotFoundException` (→ 400 via middleware) if not found
  - Create `GroupMembership.Create(spaceId, group.Id, person.Id, isOwner: true)` and add to context in the same `SaveChangesAsync` call
  - Pass `created_by_user_id` to `Group.Create()`
  - File: `apps/api/Jobuler.Application/Groups/Commands/CreateGroupCommand.cs`
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 5. Fix `RemovePersonFromGroupCommand` — owner protection
  - Before removal, check `membership.IsOwner`; if true throw `InvalidOperationException("Cannot remove the group owner. Transfer ownership first.")`
  - File: `apps/api/Jobuler.Application/Groups/Commands/LeaveGroupCommand.cs`
  - _Requirements: 3.1, 3.3, 3.4_

- [x] 6. Update `GetGroupsQuery` — filter deleted, add `ownerPersonId`
  - Add `DeletedAt == null` filter to the groups query
  - Add `OwnerPersonId Guid?` to `GroupDto` / `GroupWithMemberCountDto` — resolved via join on `GroupMemberships` where `IsOwner = true`
  - File: `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs`
  - _Requirements: 2.4, 6.4, 6.7_

- [x] 7. Update `GetGroupMembersQuery` — add `isOwner`
  - Add `IsOwner bool` field to `GroupMemberDto`
  - Include `IsOwner` in the projection from the join
  - File: `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs`
  - _Requirements: 2.3_

- [ ] 8. New commands — group lifecycle management
  - [x] 8.1 Create `RenameGroupCommand` and handler
    - New file: `apps/api/Jobuler.Application/Groups/Commands/RenameGroupCommand.cs`
    - Record: `RenameGroupCommand(Guid SpaceId, Guid GroupId, Guid RequestingUserId, string NewName)`
    - Handler: load group (404 if missing), verify caller is owner (403 if not), call `group.Rename(NewName)`, save
    - Add `RenameGroupCommandValidator` using FluentValidation: `NewName` not empty, max 100 chars, not whitespace-only
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 8.2 Create `SoftDeleteGroupCommand` and handler
    - New file: `apps/api/Jobuler.Application/Groups/Commands/SoftDeleteGroupCommand.cs`
    - Record: `SoftDeleteGroupCommand(Guid SpaceId, Guid GroupId, Guid RequestingUserId)`
    - Handler: load group (404 if missing), verify caller is owner (403 if not), call `group.SoftDelete()`, save
    - _Requirements: 6.2, 6.3, 6.6_

  - [x] 8.3 Create `RestoreGroupCommand` and handler
    - New file: `apps/api/Jobuler.Application/Groups/Commands/RestoreGroupCommand.cs`
    - Record: `RestoreGroupCommand(Guid SpaceId, Guid GroupId, Guid RequestingUserId)`
    - Handler: load group including soft-deleted (404 if missing), verify caller is owner (403 if not), verify `DeletedAt` within 30 days (400 if expired), call `group.Restore()`, save; for each membership with a linked user create a `Notification` and call `IEmailSender.SendAsync`
    - _Requirements: 7.3, 7.4, 7.5, 7.7, 7.8_

  - [x] 8.4 Create `GetDeletedGroupsQuery` and handler
    - New file: `apps/api/Jobuler.Application/Groups/Queries/GetDeletedGroupsQuery.cs`
    - Record: `GetDeletedGroupsQuery(Guid SpaceId, Guid RequestingUserId)` → `IRequest<List<DeletedGroupDto>>`
    - `DeletedGroupDto(Guid Id, string Name, DateTime DeletedAt)`
    - Handler: return groups where `DeletedAt IS NOT NULL && DeletedAt > UtcNow - 30 days` and caller is the owner
    - _Requirements: 7.1, 7.2, 7.5, 7.6_

- [x] 9. New commands — ownership transfer
  - [x] 9.1 Create `ConflictException`
    - New file: `apps/api/Jobuler.Application/Common/ConflictException.cs`
    - `public class ConflictException : InvalidOperationException` with message constructor
    - Add mapping in `ExceptionHandlingMiddleware`: `ConflictException` → HTTP 409
    - _Requirements: 8.5_

  - [x] 9.2 Create `InitiateOwnershipTransferCommand` and handler
    - New file: `apps/api/Jobuler.Application/Groups/Commands/InitiateOwnershipTransferCommand.cs`
    - Record: `InitiateOwnershipTransferCommand(Guid SpaceId, Guid GroupId, Guid CurrentOwnerUserId, Guid ProposedPersonId)`
    - Handler: verify caller is owner (403 if not), verify `ProposedPersonId` is a group member (400 if not), check no pending transfer exists (throw `ConflictException` → 409 if one does), create `PendingOwnershipTransfer`, save, resolve proposed person's linked user email, call `IEmailSender.SendAsync` with confirmation link, write audit log `action = "ownership_transfer_initiated"`
    - _Requirements: 8.3, 8.4, 8.5, 8.7_

  - [x] 9.3 Create `ConfirmOwnershipTransferCommand` and handler
    - New file: `apps/api/Jobuler.Application/Groups/Commands/ConfirmOwnershipTransferCommand.cs`
    - Record: `ConfirmOwnershipTransferCommand(string ConfirmationToken)`
    - Handler (single transaction): load pending transfer by token (400 if not found or expired), set `currentOwner.IsOwner = false`, set `newOwner.IsOwner = true`, delete `PendingOwnershipTransfer`, save all in one `SaveChangesAsync`, write audit log `action = "ownership_transfer_confirmed"`
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 9.4 Create `CancelOwnershipTransferCommand` and handler
    - New file: `apps/api/Jobuler.Application/Groups/Commands/CancelOwnershipTransferCommand.cs`
    - Record: `CancelOwnershipTransferCommand(Guid SpaceId, Guid GroupId, Guid RequestingUserId)`
    - Handler: verify caller is owner (403 if not), load pending transfer (404 if not found), delete record, save
    - _Requirements: 10.1, 10.2, 10.3_

- [x] 10. Controller — add all new endpoints, pass `CurrentUserId` to `CreateGroup`
  - Update `CreateGroup` action to pass `CurrentUserId` to `CreateGroupCommand`
  - Add `PATCH /spaces/{spaceId}/groups/{groupId}/name` → `RenameGroupCommand` (requires `people.manage`)
  - Add `DELETE /spaces/{spaceId}/groups/{groupId}` → `SoftDeleteGroupCommand` (requires `people.manage`)
  - Add `POST /spaces/{spaceId}/groups/{groupId}/restore` → `RestoreGroupCommand` (requires `people.manage`)
  - Add `GET /spaces/{spaceId}/groups/deleted` → `GetDeletedGroupsQuery` (requires `people.manage`)
  - Add `POST /spaces/{spaceId}/groups/{groupId}/transfer` → `InitiateOwnershipTransferCommand` (requires `people.manage`)
  - Add `DELETE /spaces/{spaceId}/groups/{groupId}/transfer` → `CancelOwnershipTransferCommand` (requires `people.manage`)
  - Add `GET /groups/confirm-transfer` → `ConfirmOwnershipTransferCommand` with `[AllowAnonymous]`, reads `?token=` query param
  - Add request records: `RenameGroupRequest(string Name)`, `TransferOwnershipRequest(Guid ProposedPersonId)`
  - File: `apps/api/Jobuler.Api/Controllers/GroupsController.cs`
  - _Requirements: 1.3, 4.1, 6.2, 7.3, 8.3, 9.2, 10.1_

- [x] 11. `AppDbContext` — register `PendingOwnershipTransfer` DbSet
  - Add `public DbSet<PendingOwnershipTransfer> PendingOwnershipTransfers => Set<PendingOwnershipTransfer>();`
  - Add EF configuration for `PendingOwnershipTransfer` in Infrastructure (table name, column mappings, indexes)
  - File: `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` and corresponding EF config file
  - _Requirements: 8.3_

- [x] 12. Seed data — add Dana user
  - Add Dana user, person, space membership, and `space.view` permission grant to `infra/scripts/seed.sql`
  - User UUID: `f0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c`, Person UUID: `e1a2b3c4-d5e6-4f7a-8b9c-0d1e2f3a4b5c`
  - Space UUID: `e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9`, granted by admin `a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5`
  - Do NOT add any `group_memberships` rows for Dana
  - _Requirements: 11.1, 11.2, 11.3, 11.4_

- [x] 13. Frontend — update DTOs in `lib/api/groups.ts`
  - Add `ownerPersonId: string | null` to `GroupWithMemberCountDto`
  - Add `isOwner: boolean` to `GroupMemberDto`
  - Add `DeletedGroupDto` interface: `{ id: string; name: string; deletedAt: string }`
  - File: `apps/web/lib/api/groups.ts`
  - _Requirements: 2.3, 2.4_

- [x] 14. Frontend — add new API functions in `lib/api/groups.ts`
  - `renameGroup(spaceId, groupId, name): Promise<void>` — `PATCH .../name`
  - `softDeleteGroup(spaceId, groupId): Promise<void>` — `DELETE .../groups/{groupId}`
  - `restoreGroup(spaceId, groupId): Promise<void>` — `POST .../restore`
  - `getDeletedGroups(spaceId): Promise<DeletedGroupDto[]>` — `GET .../groups/deleted`
  - `initiateOwnershipTransfer(spaceId, groupId, proposedPersonId): Promise<void>` — `POST .../transfer`
  - `cancelOwnershipTransfer(spaceId, groupId): Promise<void>` — `DELETE .../transfer`
  - File: `apps/web/lib/api/groups.ts`
  - _Requirements: 4.1, 6.2, 7.3, 8.3, 10.1_

- [x] 15. Frontend — group avatar utility
  - Create `apps/web/lib/utils/groupAvatar.ts`
  - Implement `getAvatarColor(name: string): string` — deterministic color from name using char code sum mod 8 over a fixed palette; return `"#94A3B8"` for empty/null
  - Implement `getAvatarLetter(name: string): string` — first char uppercased; return `"?"` for empty/null
  - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x]* 15.1 Write property tests for avatar utility (fast-check)
    - **Property 11: Avatar color is deterministic** — for any string, `getAvatarColor(name)` called twice returns the same value
    - **Property 12: Avatar letter is uppercase first character** — for any non-empty string returns `name[0].toUpperCase()`; for empty/null returns `"?"`
    - File: `apps/web/__tests__/groupAvatar.test.ts`
    - **Validates: Requirements 5.2, 5.1, 5.3, 5.4**

- [x] 16. Frontend — group list page: add avatar to cards
  - Create a `GroupAvatar` component (inline or in `components/groups/GroupAvatar.tsx`) using `getAvatarColor` / `getAvatarLetter`
  - Replace the generic SVG icon in each group card on `app/groups/page.tsx` with `GroupAvatar`
  - _Requirements: 5.1, 5.2, 5.3, 5.4_

- [x] 17. Frontend — group detail page: header avatar, owner protections, settings tab owner actions
  - Add `GroupAvatar` next to the group name in the header of `app/groups/[groupId]/page.tsx`
  - In `renderMembersEdit()`: hide the "הסר" button when `m.isOwner === true`
  - In `renderSettingsPanel()` (owner-only, gated on `group.ownerPersonId === currentPersonId`):
    - Inline rename field with save button calling `renameGroup`; update displayed name on success
    - "מחק קבוצה" button → confirmation dialog ("האם אתה בטוח? ניתן לשחזר תוך 30 יום") → `softDeleteGroup` → redirect to `/groups`
    - "קבוצות מחוקות" section: fetch `getDeletedGroups` on settings tab open, list with restore buttons calling `restoreGroup`
    - Ownership transfer section: dropdown of non-owner members + initiate button calling `initiateOwnershipTransfer`; show "ממתין לאישור" status + "בטל העברה" button calling `cancelOwnershipTransfer` when a pending transfer exists
  - _Requirements: 3.2, 4.4, 4.5, 6.1, 7.1, 7.2, 8.1, 8.2, 8.6, 10.4_

- [x] 18. Frontend — new confirm-transfer page
  - Create `apps/web/app/groups/confirm-transfer/page.tsx`
  - Public page (no auth required) — do NOT use `apiClient`; use plain `fetch`
  - Read `?token=` from `useSearchParams()`
  - On mount call `GET /groups/confirm-transfer?token=...` with plain `fetch`
  - Show success state: "הבעלות הועברה בהצלחה" on HTTP 200
  - Show error state in Hebrew for invalid/expired token (HTTP 400) or network error
  - _Requirements: 9.2, 9.3_

- [ ] 19. Checkpoint — ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x]* 19.1 Write property tests P1–P10 (FsCheck, C# backend)
  - **Property 1:** Creator auto-membership — for any valid `CreateGroupCommand`, creator's person has `IsOwner = true` after execution
    - **Validates: Requirements 1.1, 1.2, 1.4**
  - **Property 2:** Exactly one owner per group — after any sequence of valid operations, count of `IsOwner = true` memberships per group is exactly 1
    - **Validates: Requirements 2.1, 2.5**
  - **Property 3:** Owner removal rejected — `RemovePersonFromGroupCommand` for an owner always throws `InvalidOperationException`
    - **Validates: Requirements 3.1, 3.3, 3.4**
  - **Property 4:** Soft-deleted groups excluded from `GetGroupsQuery`
    - **Validates: Requirements 6.4, 6.7**
  - **Property 5:** Soft-delete preserves all membership rows (count unchanged)
    - **Validates: Requirements 6.6**
  - **Property 6:** Soft-delete / restore round trip restores visibility in `GetGroupsQuery`
    - **Validates: Requirements 6.8, 7.3**
  - **Property 7:** `GetDeletedGroupsQuery` respects 30-day window
    - **Validates: Requirements 7.2, 7.5**
  - **Property 8:** Restore triggers exactly M notifications for M linked members
    - **Validates: Requirements 7.7, 7.8**
  - **Property 9:** Non-owner rejection — all owner-only commands throw `UnauthorizedAccessException` for non-owners
    - **Validates: Requirements 4.2, 6.3, 7.4, 10.2**
  - **Property 10:** Rename rejects blank or >100-char names
    - **Validates: Requirements 4.3**
  - Tag each test: `// Feature: group-ownership, Property {N}: {property_text}`
  - Minimum 100 iterations per property
  - File: `apps/api/Jobuler.Tests/Groups/GroupOwnershipPropertyTests.cs`

- [x]* 19.2 Write property tests P13–P15 (FsCheck, C# backend)
  - **Property 13:** At most one pending transfer per group — second `InitiateOwnershipTransferCommand` throws `ConflictException`
    - **Validates: Requirements 8.5**
  - **Property 14:** Expired tokens rejected — `ConfirmOwnershipTransferCommand` with past `ExpiresAt` throws `InvalidOperationException`
    - **Validates: Requirements 9.1, 9.3**
  - **Property 15:** Atomic ownership swap — after confirm, new owner has `IsOwner = true`, previous has `IsOwner = false`, transfer record deleted
    - **Validates: Requirements 9.2, 9.5**
  - File: `apps/api/Jobuler.Tests/Groups/GroupOwnershipPropertyTests.cs`

- [x]* 19.3 Write property test P16 (fast-check, TypeScript frontend)
  - **Property 16:** Transfer dropdown excludes the owner — for any member list with exactly one `isOwner = true`, the dropdown renders only members where `isOwner = false`
    - **Validates: Requirements 8.2**
  - File: `apps/web/__tests__/transferDropdown.test.tsx`

- [ ] 20. Step documentation
  - Create `docs/steps/029-group-ownership.md`
  - Include: title, phase, purpose, files created/modified, key decisions, how it connects, how to verify, what comes next, git commit command
  - _Requirements: all_

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- `ConflictException` (task 9.1) must be implemented before the transfer commands that throw it
- The confirm-transfer page (task 18) uses plain `fetch`, not `apiClient`, to avoid auth headers — this is intentional
- `GetGroupsQuery` currently filters by `g.IsActive`; the new filter adds `g.DeletedAt == null` alongside it
- Dana's person record needs `linked_user_id` set — check that the `people` table has this column (added in a prior migration)
- Property tests run a minimum of 100 iterations each; use `FsCheck` for C# and `fast-check` for TypeScript
