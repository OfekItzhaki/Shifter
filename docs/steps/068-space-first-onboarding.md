# 068 — Space-First Onboarding

## Phase
Phase 3 — Multi-tenancy & Onboarding

## Purpose
Changes the user journey so that new users create or join a Space (organization) before creating groups. Adds space invite codes, parent-child group linking, space switching, and migration for existing users.

## What was built

### Backend (Domain)
- `apps/api/Jobuler.Domain/Spaces/Space.cs` — Added `InviteCode` property, `RegenerateInviteCode()`, auto-generates on create
- `apps/api/Jobuler.Domain/Groups/Group.cs` — Added `ParentGroupId`, `SetParentGroup()`, `UnlinkFromParent()`
- `apps/api/Jobuler.Domain/Spaces/UserSpaceMigration.cs` — New entity for tracking one-time migrations

### Backend (Application)
- `JoinSpaceByInviteCodeCommand` — Join space via 8-char code
- `UpdateSpaceCommand` — Update space name/description/locale (owner only)
- `RegenerateSpaceInviteCodeCommand` — Regenerate invite code (owner only)
- `MigrateUserSpaceCommand` — Auto-create space for existing users with groups
- `GetSpaceDetailQuery` — Extended space info with member/group counts, invite code
- `GetSpaceMembersQuery` — List space members

### Backend (API)
- `SpacesController` — 5 new endpoints: join, update, regenerate, members, migrate

### Database Migrations
- `068_space_invite_code.sql` — invite_code column + unique index + backfill
- `069_parent_group_id.sql` — parent_group_id column + index
- `070_user_space_migrations.sql` — migration tracking table

### Frontend
- `app/onboarding/page.tsx` — Space creation/join wizard
- `app/spaces/settings/page.tsx` — Space management page
- `components/shell/SpaceSwitcher.tsx` — Space switching dropdown
- `components/groups/LinkedGroupSelector.tsx` — Parent group selector
- `lib/api/spaces.ts` — Extended with all new API calls
- `components/shell/AppShell.tsx` — Integrated SpaceSwitcher, redirect logic
- `lib/onboarding/storage.ts` — Per-space onboarding state support

### i18n
- `messages/en.json` — Added `spaceOnboarding` + extended `spaces` section
- `messages/he.json` — Hebrew translations for all new keys
- `messages/ru.json` — Russian translations for all new keys

### Fixes
- `app/settings/page.tsx` — Country list now uses active UI locale, dark mode support, dynamic RTL
- `.gitignore` — Added `docs/SERVER_SETUP.md`

## Key decisions
- Space invite codes are 8-char uppercase alphanumeric (same pattern as group join codes)
- Parent-child groups limited to single level (no deep nesting)
- Migration is triggered client-side on login, runs once per user
- Onboarding state is now per-user-per-space in localStorage
- Space switcher only shows dropdown when user has multiple spaces

## How it connects
- Builds on existing Space/SpaceMembership/SpacePermissionGrant entities
- Uses existing MediatR CQRS pattern for all new commands/queries
- Integrates with existing AppShell sidebar and routing
- Solver integration for parent schedule cascading is planned (Task 16)

## How to run / verify
1. Register a new user → should redirect to `/onboarding`
2. Create a space → should redirect to `/home` with space selected
3. Check sidebar → SpaceSwitcher shows current space name
4. Go to `/spaces/settings` → see invite code, members, edit name
5. Existing users with groups → migration creates a default space on login

## What comes next
- Solver integration: parent schedule cascading to child groups
- Full linked group UI in group settings tab
- E2E tests for onboarding flow

## Git commit

```bash
git add -A && git commit -m "feat(spaces): space-first onboarding, invite codes, parent groups, migration"
```
