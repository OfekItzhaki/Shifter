# 204 — Invitation Flow Preservation Tests

## Phase
Bugfix — invitation-flow-fixes

## Purpose
Write property-based preservation tests that verify existing correct behavior (non-buggy paths) BEFORE implementing the fix. These tests guard against regressions by ensuring that:
1. Users who already have a SpaceMembership don't get duplicates when joining a group
2. Users already in a group can re-join without creating duplicate GroupMembership records
3. AddPersonByEmail for a user who already has SpaceMembership doesn't create duplicates

## What was built
- `apps/api/Jobuler.Tests/InvitationFlow/PreservationTests.cs` — Three preservation tests that pass on unfixed code and must continue to pass after the fix

## Key decisions
- Used the same test setup pattern as `BugConditionExplorationTests.cs` (InMemory EF Core, reflection-based ID seeding)
- Tests focus on the non-buggy path: user already has SpaceMembership → no duplicate created
- Tests verify both SpaceMembership idempotency and GroupMembership idempotency
- All three tests pass on unfixed code, confirming baseline behavior

## How it connects
- Depends on: Task 1 (BugConditionExplorationTests established the test pattern)
- Required by: Tasks 3.5 (re-run preservation tests after fix to confirm no regressions)

## How to run / verify
```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~Jobuler.Tests.InvitationFlow.PreservationTests" --verbosity normal
```
All 3 tests should pass.

## What comes next
Task 3: Fix backend handlers to add SpaceMembership + space.view permission grant

## Git commit
```bash
git add -A && git commit -m "test(bugfix): add invitation flow preservation tests for regression prevention"
```
