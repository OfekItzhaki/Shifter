# 205 — Invitation Flow Backend Fix: SpaceMembership + SpaceView Permission

## Phase

Bugfix — Invitation Flow Fixes

## Purpose

Users joining groups via join code, email invitation, or phone invitation were getting 403 errors on all subsequent space-scoped API calls because the handlers created Person + GroupMembership but never created a SpaceMembership or granted the `space.view` permission. The PermissionService requires a SpaceMembership to authorize any space-scoped call.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Groups/Commands/JoinCodeCommands.cs` | Added `using Jobuler.Domain.Spaces;`. After GroupMembership creation, checks if SpaceMembership exists; if not, creates one and grants `space.view` permission. |
| `apps/api/Jobuler.Application/Groups/Commands/AddPersonByEmailCommand.cs` | Added `using Jobuler.Domain.Spaces;`. After step 6 (notification), if `user is not null`, checks and creates SpaceMembership + `space.view` grant. |
| `apps/api/Jobuler.Application/Groups/Commands/AddPersonByPhoneCommand.cs` | Added `using Jobuler.Domain.Spaces;`. Same pattern as email handler — ensures SpaceMembership for linked users. |

## Key decisions

- **Idempotent check**: All three handlers check `SpaceMemberships.Any(...)` before creating, preventing duplicates if the user already has a SpaceMembership.
- **Permission grant**: `space.view` is granted alongside SpaceMembership creation so the user can immediately access the space.
- **GrantedByUserId**: For join-by-code, the user grants to themselves (`req.UserId`). For email/phone invitations, the requesting user (admin) is recorded as the granter (`req.RequestingUserId`).
- **Placement**: The SpaceMembership logic is placed after GroupMembership creation but before the final `SaveChangesAsync`, so everything is persisted in a single transaction.

## How it connects

- Fixes bugs 1.1 and 1.4 from the bugfix spec (missing SpaceMembership on group join)
- The `PermissionService` checks `SpacePermissionGrants` to authorize API calls — without the `space.view` grant, users get 403
- Preservation tests confirm no duplicate SpaceMemberships are created for users who already have one

## How to run / verify

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~Jobuler.Tests.InvitationFlow.BugConditionExplorationTests"
dotnet test --filter "FullyQualifiedName~Jobuler.Tests.InvitationFlow.PreservationTests"
```

All 6 bug condition tests should PASS (confirming the fix works).
All 3 preservation tests should PASS (confirming no regressions).

## What comes next

- Task 3.4: Re-run bug condition exploration tests to formally verify the fix (done here)
- Task 3.5: Re-run preservation tests to formally verify no regressions (done here)
- Task 4: Fix frontend register page redirect preservation
- Task 5: Fix frontend API client 401 handling

## Git commit

```bash
git add -A && git commit -m "fix(invitation-flow): add SpaceMembership + space.view grant in join/invite handlers"
```
