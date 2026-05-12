# 143 — Group Invitation Links

## Phase
Phase 8 — Collaboration

## Purpose
Admins need a simple way to invite people to their group without manually adding each person. A shareable join code (like "ABCD1234") lets anyone with the code join the group instantly.

## What was built

### Backend:

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Groups/Group.cs` | Added `JoinCode` property, `RegenerateJoinCode()` method, auto-generates on creation |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs` | Added `join_code` column mapping with unique index |
| `apps/api/Jobuler.Application/Groups/Commands/JoinCodeCommands.cs` | Three handlers: GetJoinCode, RegenerateJoinCode, JoinGroupByCode |
| `apps/api/Jobuler.Api/Controllers/GroupsController.cs` | Three new endpoints: GET join-code, POST regenerate, POST /groups/join |
| `infra/migrations/038_group_join_code.sql` | Migration to add join_code column and backfill existing groups |

### Frontend:

| File | Description |
|------|-------------|
| `apps/web/lib/api/groups.ts` | Added `getJoinCode`, `regenerateJoinCode`, `joinGroupByCode` API functions |
| `apps/web/app/groups/join/page.tsx` | Join page — enter code, join group, handles auth redirect |
| `apps/web/messages/{en,he,ru}.json` | Added `groups.join.*` translations |

## API Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/spaces/{id}/groups/{id}/join-code` | Admin | Get the group's join code |
| POST | `/spaces/{id}/groups/{id}/join-code/regenerate` | Admin | Generate a new code (invalidates old) |
| POST | `/groups/join` | Any user | Join a group using a code |

## Join Code Format
- 8 uppercase alphanumeric characters (e.g., "A1B2C3D4")
- Generated from UUID, guaranteed unique via DB index
- Auto-generated on group creation
- Can be regenerated (old code stops working)

## Flow

1. Admin goes to group settings → copies the join code
2. Shares it via WhatsApp/message/verbally
3. Team member opens `shifter.ofeklabs.com/groups/join?code=ABCD1234`
4. If not logged in → redirected to login/register with return URL
5. If logged in → instantly joins the group
6. Redirected to the group page

## Key decisions

1. **Simple 8-char code** — Easy to share verbally or via text. No long URLs needed.
2. **No expiry** — Codes don't expire. Admin can regenerate to invalidate.
3. **Auto-creates person** — If the user doesn't have a Person record in the space, one is created automatically.
4. **Idempotent** — Joining twice with the same code is safe (no duplicate membership).

## How to run / verify
1. Run migration: `038_group_join_code.sql`
2. Create a group → it gets a join code
3. GET `/spaces/{id}/groups/{id}/join-code` → returns the code
4. Open `/groups/join?code=XXXX` in another browser/account
5. Click "Join Group" → user is added to the group

## Git commit

```bash
git add -A && git commit -m "feat(phase8): group invitation links — shareable join codes for easy onboarding"
```
