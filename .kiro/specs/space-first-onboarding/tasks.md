# Tasks

## Task 1: Database Schema Changes
- [x] Add `invite_code VARCHAR(8) UNIQUE` column to `spaces` table
- [x] Add `parent_group_id UUID REFERENCES groups(id) ON DELETE SET NULL` column to `groups` table
- [x] Create `user_space_migrations` table with `id`, `user_id` (unique), `space_id`, `migrated_at`, `groups_migrated`
- [x] Generate invite codes for all existing spaces (backfill migration)

## Task 2: Domain Layer — Space Invite Code
- [x] Add `InviteCode` property to `Space.cs` entity
- [x] Generate invite code in `Space.Create()` factory method
- [x] Add `RegenerateInviteCode()` method to Space entity
- [x] Add `GenerateInviteCode()` private helper (8-char uppercase alphanumeric)

## Task 3: Domain Layer — Parent Group Linking
- [x] Add `ParentGroupId` property to `Group.cs` entity
- [x] Add `SetParentGroup(Guid? parentGroupId)` method
- [x] Add `UnlinkFromParent()` method

## Task 4: Domain Layer — UserSpaceMigration Entity
- [x] Create `UserSpaceMigration.cs` in `Jobuler.Domain/Spaces/`
- [x] Add `UserId`, `SpaceId`, `MigratedAt`, `GroupsMigrated` properties
- [x] Add `Create()` factory method
- [x] Register entity in `AppDbContext` (DbSet + Fluent API config)

## Task 5: API — Join Space by Invite Code
- [x] Create `JoinSpaceByInviteCodeCommand` (InviteCode, UserId)
- [x] Create handler: find space by invite code, check not already member, create membership + grant `space.view`
- [x] Create `JoinSpaceByInviteCodeValidator` (code must be 8 alphanumeric chars)
- [x] Add `POST /spaces/join` endpoint to `SpacesController`

## Task 6: API — Space Management Endpoints
- [x] Create `UpdateSpaceCommand` (SpaceId, Name, Description, Locale, RequestingUserId)
- [x] Create handler: verify owner, validate name 2-100 chars, persist changes
- [x] Create `UpdateSpaceValidator`
- [x] Create `RegenerateSpaceInviteCodeCommand` (SpaceId, RequestingUserId)
- [x] Create handler: verify owner, regenerate code, return new code
- [x] Create `GetSpaceMembersQuery` (SpaceId) and handler
- [x] Create `GetSpaceDetailQuery` (SpaceId, RequestingUserId) and handler (include invite code, member count, group count)
- [x] Add `PUT /spaces/{spaceId}` endpoint
- [x] Add `POST /spaces/{spaceId}/invite-code/regenerate` endpoint
- [x] Add `GET /spaces/{spaceId}/members` endpoint
- [x] Update `GET /spaces/{spaceId}` to return extended detail

## Task 7: API — Link Parent Group
- [x] Create `LinkParentGroupCommand` (SpaceId, ChildGroupId, ParentGroupId, RequestingUserId)
- [x] Create handler: validate same space, single-level hierarchy, no circular refs, set parent
- [x] Create `UnlinkParentGroupCommand` (SpaceId, ChildGroupId, RequestingUserId)
- [x] Create handler: verify ownership, set parent_group_id to null
- [x] Create `LinkParentGroupValidator`
- [x] Add `POST /spaces/{spaceId}/groups/{groupId}/link-parent` endpoint
- [x] Add `DELETE /spaces/{spaceId}/groups/{groupId}/link-parent` endpoint

## Task 8: API — Migration Service
- [x] Create `MigrateUserSpaceCommand` (UserId)
- [x] Create handler: check no existing migration, create space, assign groups, record migration
- [x] Wrap in transaction with rollback on failure
- [x] Add `POST /spaces/migrate` endpoint
- [x] Log migration results for operational review

## Task 9: Frontend — API Client Extensions
- [x] Add `joinSpaceByCode(inviteCode)` to `lib/api/spaces.ts`
- [x] Add `getSpaceDetail(spaceId)` to `lib/api/spaces.ts`
- [x] Add `updateSpace(spaceId, data)` to `lib/api/spaces.ts`
- [x] Add `regenerateInviteCode(spaceId)` to `lib/api/spaces.ts`
- [x] Add `getSpaceMembers(spaceId)` to `lib/api/spaces.ts`
- [x] Add `linkParentGroup(spaceId, groupId, parentGroupId)` to `lib/api/spaces.ts`
- [x] Add `unlinkParentGroup(spaceId, groupId)` to `lib/api/spaces.ts`
- [x] Add `migrateUserSpace()` to `lib/api/spaces.ts`

## Task 10: Frontend — Onboarding Wizard Page
- [x] Create `app/onboarding/page.tsx` with `SpaceOnboardingWizard` component
- [x] Step 1: Choose "Create New Space" or "Join Existing Space"
- [x] Step 2a (Create): Space name input with validation (2-100 chars), loading state, error handling
- [x] Step 2b (Join): Invite code input (8 chars), validation, rate limiting (5 attempts → 60s cooldown)
- [x] On success: set space in spaceStore, navigate to /home
- [x] Support i18n (he, en, ru) for all wizard text
- [x] Add translations to `messages/en.json`, `messages/he.json`, `messages/ru.json`

## Task 11: Frontend — Space Switcher Component
- [x] Create `components/shell/SpaceSwitcher.tsx`
- [x] Show current space name in sidebar (truncated at 30 chars)
- [x] Dropdown with all user's spaces when clicked (only if multiple spaces)
- [x] On switch: update spaceStore, invalidate cached data, refetch
- [x] Handle invalid persisted space (clear and show list)
- [x] Add "+ Create New Space" option at bottom of dropdown
- [x] Integrate into `AppShell.tsx` sidebar

## Task 12: Frontend — Space Settings Page
- [x] Create `app/spaces/settings/page.tsx`
- [x] Space name edit (2-100 chars validation)
- [x] Space description edit (0-500 chars)
- [x] Invite code display with copy button
- [x] Regenerate invite code button (with confirmation)
- [x] Member list display
- [x] Only show edit controls to space owner
- [x] Support dark mode and i18n

## Task 13: Frontend — Redirect Logic
- [x] Update app routing to check space membership on load
- [x] If no spaces → redirect to /onboarding
- [x] If has spaces → ensure valid space selected, redirect to /home
- [x] If on /onboarding but has spaces → redirect to /home
- [x] Handle edge case: user removed from all spaces
- [x] Trigger migration check for existing users without spaces but with groups

## Task 14: Frontend — Linked Group UI
- [x] Create `components/groups/LinkedGroupSelector.tsx` dropdown
- [x] Add parent group selector to group settings tab (admin only)
- [x] Show parent/child relationship in group list view
- [x] Handle unlink action
- [x] Validate single-level hierarchy in UI

## Task 15: Update Onboarding Store for Per-Space State
- [x] Change onboarding storage key to include spaceId: `shifter-onboarding-${userId}-${spaceId}`
- [x] Update `hydrate()`, `completeStep()`, `reset()`, `dismiss()` to accept spaceId
- [x] Update `OnboardingProvider` to pass current spaceId
- [x] Ensure setup guide works independently per space

## Task 16: Solver Integration — Parent Schedule Cascading
- [x] When building solver payload for a child group, include parent's published assignments
- [x] Add `parent_schedule` field to solver input payload
- [x] Update `SolverPayloadNormalizer` to fetch parent schedule data
- [x] Solver uses parent assignments as constraints (no conflicts)
