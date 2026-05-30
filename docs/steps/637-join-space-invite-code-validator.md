# 637 — JoinSpaceByInviteCodeCommandValidator

## Phase
Space-First Onboarding

## Purpose
Validates the `JoinSpaceByInviteCodeCommand` input before the handler executes. Ensures the invite code is exactly 8 alphanumeric characters and the user ID is a non-empty GUID.

## What was built
- `apps/api/Jobuler.Application/Spaces/Validators/JoinSpaceByInviteCodeCommandValidator.cs` — FluentValidation validator for the join-by-invite-code command

## Key decisions
- Followed existing validator naming convention (`{CommandName}Validator`)
- Placed in `Spaces/Validators/` alongside other space validators
- Accepts both upper and lowercase alphanumeric codes (`^[A-Za-z0-9]+$`)
- Uses `.Length(8)` for exact length enforcement

## How it connects
- Validates input for `JoinSpaceByInviteCodeCommand` before the handler runs
- Part of the FluentValidation pipeline registered via DI (auto-discovered by assembly scanning)
- Works with the `JoinSpaceByInviteCodeCommandHandler` which finds the space by invite code

## How to run / verify
```bash
cd apps/api
dotnet build Jobuler.Application
```

## What comes next
- `POST /spaces/join` endpoint in `SpacesController` that dispatches this command

## Git commit
```bash
git add -A && git commit -m "feat(spaces): add JoinSpaceByInviteCodeCommandValidator"
```
