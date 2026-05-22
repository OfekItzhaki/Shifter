# Tasks

## Task 1: Database Schema Changes
- [~] Add `invite_code VARCHAR(8) UNIQUE` column to `spaces` table
- [~] Add `parent_group_id UUID REFERENCES groups(id) ON DELETE SET NULL` column to `groups` table
- [~] Create `user_space_migrations` table with `id`, `user_id` (unique), `space_id`, `migrated_at`, `groups_migrated`
- [~] Generate invite codes for all existing spaces (backfill migration)

## Task 2: Domain Layer — Space Invite Code
- [~] Add `InviteCode` property to `Space.cs` entity
- [~] Generate invite code in `Space.Create()` factory method
- [~] Add `RegenerateInviteCode()` method to Space entity
- [~] Add `GenerateInviteCode()` private helper (8-char uppercase alphanumeric)

## Task 3: Domain Layer — Parent Group Linking
- [~] Add `ParentGroupId` property to `Group.cs` entity
- [~] Add `SetParentGroup(Guid? parentGroupId)` method
- [~] Add `UnlinkFromParent()` method

## Task 4: Domain Layer — UserSpaceMigration Entity
- [~] Create `UserSpaceMigration.cs` in `Jobuler.Domain/Spaces/`
- [~] Add `UserId`, `SpaceId`, `MigratedAt`, `GroupsMigrated` properties
- [~] Add `Create()` factory method
- [~] Register entity in `AppDbContext` (DbSet + Fluent API config)

## Task 5: API — Join Space by Invite Code
- [~] Create `JoinSpaceByInviteCodeCommand` (InviteCode, UserId)
- [~] Create handler: find space by invite code, check not already member, create membership + grant `space.view`
- [~] Create `JoinSpaceByInviteCodeValidator` (code must be 8 alphanumeric chars)
- [~] Add `POST /spaces/join` endpoint to `SpacesController`

## Task 6: API — Space Management Endpoints
- [~] Create `UpdateSpaceCommand` (SpaceId, Name, Description, Locale, RequestingUserId)
- [~] Create handler: verify owner, validate name 2-100 chars, persist changes
- [~] Create `UpdateSpaceValidator`
- [~] Create `RegenerateSpaceInviteCodeCommand` (SpaceId, RequestingUserId)
- [~] Create handler: verify owner, regenerate code, return new code
- [~] Create `GetSpaceMembersQuery` (SpaceId) and handler
- [~] Create `GetSpaceDetailQuery` (SpaceId, RequestingUserId) and handler (include invite code, member count, group count)
- [~] Add `PUT /spaces/{spaceId}` endpoint
- [~] Add `POST /spaces/{spaceId}/invite-code/regenerate` endpoint
- [~] Add `GET /spaces/{spaceId}/members` endpoint
- [~] Update `GET /spaces/{spaceId}` to return extended detail

## Task 7: API — Link Parent Group
- [~] Create `LinkParentGroupCommand` (SpaceId, ChildGroupId, ParentGroupId, RequestingUserId)
- [~] Create handler: validate same space, single-level hierarchy, no circular refs, set parent
- [~] Create `UnlinkParentGroupCommand` (SpaceId, ChildGroupId, RequestingUserId)
- [~] Create handler: verify ownership, set parent_group_id to null
- [~] Create `LinkParentGroupValidator`
- [~] Add `POST /spaces/{spaceId}/groups/{groupId}/link-parent` endpoint
- [~] Add `DELETE /spaces/{spaceId}/groups/{groupId}/link-parent` endpoint

## Task 8: API — Migration Service
- [~] Create `MigrateUserSpaceCommand` (UserId)
- [~] Create handler: check no existing migration, create space, assign groups, record migration
- [~] Wrap in transaction with rollback on failure
- [~] Add `POST /spaces/migrate` endpoint
- [~] Log migration results for operational review

## Task 9: Frontend — API Client Extensions
- [~] Add `joinSpaceByCode(inviteCode)` to `lib/api/spaces.ts`
- [~] Add `getSpaceDetail(spaceId)` to `lib/api/spaces.ts`
- [~] Add `updateSpace(spaceId, data)` to `lib/api/spaces.ts`
- [~] Add `regenerateInviteCode(spaceId)` to `lib/api/spaces.ts`
- [~] Add `getSpaceMembers(spaceId)` to `lib/api/spaces.ts`
- [~] Add `linkParentGroup(spaceId, groupId, parentGroupId)` to `lib/api/spaces.ts`
- [~] Add `unlinkParentGroup(spaceId, groupId)` to `lib/api/spaces.ts`
- [~] Add `migrateUserSpace()` to `lib/api/spaces.ts`

## Task 10: Frontend — Onboarding Wizard Page
- [~] Create `app/onboarding/page.tsx` with `SpaceOnboardingWizard` component
- [~] Step 1: Choose "Create New Space" or "Join Existing Space"
- [~] Step 2a (Create): Space name input with validation (2-100 chars), loading state, error handling
- [~] Step 2b (Join): Invite code input (8 chars), validation, rate limiting (5 attempts → 60s cooldown)
- [~] On success: set space in spaceStore, navigate to /home
- [~] Support i18n (he, en, ru) for all wizard text
- [~] Add translations to `messages/en.json`, `messages/he.json`, `messages/ru.json`

## Task 11: Frontend — Space Switcher Component
- [~] Create `components/shell/SpaceSwitcher.tsx`
- [~] Show current space name in sidebar (truncated at 30 chars)
- [~] Dropdown with all user's spaces when clicked (only if multiple spaces)
- [~] On switch: update spaceStore, invalidate cached data, refetch
- [~] Handle invalid persisted space (clear and show list)
- [~] Add "+ Create New Space" option at bottom of dropdown
- [~] Integrate into `AppShell.tsx` sidebar

## Task 12: Frontend — Space Settings Page
- [~] Create `app/spaces/settings/page.tsx`
- [~] Space name edit (2-100 chars validation)
- [~] Space description edit (0-500 chars)
- [~] Invite code display with copy button
- [~] Regenerate invite code button (with confirmation)
- [~] Member list display
- [~] Only show edit controls to space owner
- [~] Support dark mode and i18n

## Task 13: Frontend — Redirect Logic
- [~] Update app routing to check space membership on load
- [~] If no spaces → redirect to /onboarding
- [~] If has spaces → ensure valid space selected, redirect to /home
- [~] If on /onboarding but has spaces → redirect to /home
- [~] Handle edge case: user removed from all spaces
- [~] Trigger migration check for existing users without spaces but with groups

## Task 14: Frontend — Linked Group UI
- [~] Create `components/groups/LinkedGroupSelector.tsx` dropdown
- [~] Add parent group selector to group settings tab (admin only)
- [~] Show parent/child relationship in group list view
- [~] Handle unlink action
- [~] Validate single-level hierarchy in UI

## Task 15: Update Onboarding Store for Per-Space State
- [~] Change onboarding storage key to include spaceId: `shifter-onboarding-${userId}-${spaceId}`
- [~] Update `hydrate()`, `completeStep()`, `reset()`, `dismiss()` to accept spaceId
- [~] Update `OnboardingProvider` to pass current spaceId
- [~] Ensure setup guide works independently per space

## Task 16: Solver Integration — Parent Schedule Cascading
- [~] When building solver payload for a child group, include parent's published assignments
- [~] Add `parent_schedule` field to solver input payload
- [~] Update `SolverPayloadNormalizer` to fetch parent schedule data
- [~] Solver uses parent assignments as constraints (no conflicts)
