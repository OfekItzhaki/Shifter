# 638 — Space Management Endpoints (Task 6 Fixes)

## Phase

Space-First Onboarding — Task 6: API — Space Management Endpoints

## Purpose

Verify and align all Task 6 items (UpdateSpace, RegenerateInviteCode, GetSpaceMembers, GetSpaceDetail, and their controller endpoints) with the design document specifications.

## What was built

All Task 6 items were already implemented. Two discrepancies were fixed:

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Spaces/Commands/UpdateSpaceCommand.cs` | Changed name validation from 1-100 to **2-100** chars (per design doc §4.3) |
| `apps/api/Jobuler.Application/Spaces/Validators/UpdateSpaceCommandValidator.cs` | Changed FluentValidation rule from 1-100 to **2-100** chars |
| `apps/api/Jobuler.Application/Spaces/Commands/RegenerateSpaceInviteCodeCommand.cs` | Renamed parameter from `UserId` to `RequestingUserId` (per design doc §4.1) |

## Verification summary

| # | Item | Status |
|---|------|--------|
| 1 | `UpdateSpaceCommand` (SpaceId, Name, Description, Locale, RequestingUserId) | ✅ Already implemented |
| 2 | Handler (verify owner, validate name 2-100 chars, persist) | ✅ Fixed — was 1-100, now 2-100 |
| 3 | `UpdateSpaceCommandValidator` | ✅ Fixed — was 1-100, now 2-100 |
| 4 | `RegenerateSpaceInviteCodeCommand` (SpaceId, RequestingUserId) | ✅ Fixed — renamed UserId → RequestingUserId |
| 5 | Handler (verify owner via permission service, regenerate, return code) | ✅ Already implemented |
| 6 | `GetSpaceMembersQuery` (SpaceId) + handler | ✅ Already implemented |
| 7 | `GetSpaceDetailQuery` (SpaceId, RequestingUserId) + handler | ✅ Already implemented |
| 8 | `PUT /spaces/{spaceId}` endpoint | ✅ Already implemented |
| 9 | `POST /spaces/{spaceId}/invite-code/regenerate` endpoint | ✅ Already implemented |
| 10 | `GET /spaces/{spaceId}/members` endpoint | ✅ Already implemented |
| 11 | `GET /spaces/{spaceId}` returning extended detail | ✅ Already implemented |

## Key decisions

- Name minimum length changed from 1 to 2 to match design doc specification (§4.3: "name 2–100 chars")
- Parameter naming aligned with design doc convention (`RequestingUserId` for consistency across all commands)

## How it connects

- These endpoints are consumed by the frontend Space Settings page (Task 12)
- The `GetSpaceDetailQuery` is used by the Space Switcher (Task 11) and redirect logic (Task 13)
- `RegenerateSpaceInviteCodeCommand` is used by the invite code card in Space Settings

## How to run / verify

```bash
cd apps/api && dotnet build
```

Build succeeds with no errors related to these changes.

## What comes next

- Task 7: API — Link Parent Group
- Task 9: Frontend API client extensions that call these endpoints

## Git commit

```bash
git add -A && git commit -m "fix(spaces): align Task 6 name validation to 2-100 chars and rename UserId to RequestingUserId"
```
