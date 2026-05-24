# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Missing SpaceMembership on Group Join
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists in all three handlers
  - **Scoped PBT Approach**: Scope the property to concrete failing cases — user joins group (by code, email, or phone) without existing SpaceMembership
  - Test that after `JoinGroupByCodeCommandHandler.Handle(...)` completes, `SpaceMemberships.Any(sm => sm.UserId == userId && sm.SpaceId == spaceId)` returns true (from Bug Condition in design: action IN ['joinByCode', 'addByEmail', 'addByPhone'] AND NOT hasExistingSpaceMembership)
  - Test that after `AddPersonByEmailCommandHandler.Handle(...)` completes with a linked user, SpaceMembership exists for that user
  - Test that after `AddPersonByPhoneCommandHandler.Handle(...)` completes with a linked user, SpaceMembership exists for that user
  - Test that `SpacePermissionGrants.Any(g => g.UserId == userId && g.SpaceId == spaceId && g.PermissionKey == "space.view")` returns true after each handler
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests FAIL (this is correct - it proves the bug exists: no SpaceMembership or permission grant is created)
  - Document counterexamples found (e.g., "After JoinGroupByCodeCommandHandler completes, SpaceMemberships table has no record for the user in the group's space")
  - Mark task complete when tests are written, run, and failure is 
  - _Requirements: 1.1, 1.4, 2.1, 2.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - Existing SpaceMembership Idempotency and Unchanged Flows
  - **IMPORTANT**: Follow observation-first methodology
  - **Observe on UNFIXED code**:
  - Observe: User with existing SpaceMembership joins group by code → no duplicate SpaceMembership created, GroupMembership count unchanged
  - Observe: User already in group joins again → success returned, no duplicate memberships
  - Observe: Register page without redirect param → navigates to `/login?registered=1`
  - Observe: API client receives 403 → redirects to `/error/forbidden`
  - **Write property-based tests capturing observed behavior**:
  - For all users who already have a SpaceMembership in the target space, joining a group does NOT create a duplicate SpaceMembership (count stays the same)
  - For all users already in a group, re-joining returns success without duplicate GroupMembership records
  - For all registration attempts without a redirect param, navigation target is `/login?registered=1`
  - For all 403 responses, the client redirects to `/error/forbidden` (not affected by 401 fix)
  - Verify all tests PASS on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.3, 3.4_

- [x] 3. Fix backend handlers — Add SpaceMembership + space.view permission grant

  - [x] 3.1 Fix JoinGroupByCodeCommandHandler
    - In `apps/api/Jobuler.Application/Groups/Commands/JoinCodeCommands.cs`, after creating Person + GroupMembership:
    - Check if SpaceMembership exists: `context.SpaceMemberships.Any(sm => sm.UserId == req.UserId && sm.SpaceId == group.SpaceId)`
    - If not, create: `SpaceMembership.Create(group.SpaceId, req.UserId)` and add to context
    - Grant permission: `SpacePermissionGrant.Grant(group.SpaceId, req.UserId, Permissions.SpaceView, req.UserId)` and add to context
    - Call `await context.SaveChangesAsync(cancellationToken)`
    - _Bug_Condition: isBugCondition(input) where input.action == 'joinByCode' AND NOT input.hasExistingSpaceMembership_
    - _Expected_Behavior: SpaceMembership created AND space.view permission granted_
    - _Preservation: Skip creation if SpaceMembership already exists (idempotent)_
    - _Requirements: 2.1, 3.1, 3.2_

  - [x] 3.2 Fix AddPersonByEmailCommandHandler
    - In `apps/api/Jobuler.Application/Groups/Commands/AddPersonByEmailCommand.cs`, after creating Person + GroupMembership:
    - Only if `user is not null` (invited person has an existing account):
    - Check if SpaceMembership exists for `user.Id` in `req.SpaceId`
    - If not, create SpaceMembership and grant `space.view` permission
    - _Bug_Condition: isBugCondition(input) where input.action == 'addByEmail' AND input.userId IS NOT NULL AND NOT input.hasExistingSpaceMembership_
    - _Expected_Behavior: SpaceMembership created AND space.view permission granted for linked user_
    - _Preservation: Skip if user is null (no linked account) or SpaceMembership already exists_
    - _Requirements: 2.4, 3.1_

  - [x] 3.3 Fix AddPersonByPhoneCommandHandler
    - In `apps/api/Jobuler.Application/Groups/Commands/AddPersonByPhoneCommand.cs`, after creating Person + GroupMembership:
    - Only if `user is not null` (invited person has an existing account):
    - Check if SpaceMembership exists for `user.Id` in `req.SpaceId`
    - If not, create SpaceMembership and grant `space.view` permission
    - _Bug_Condition: isBugCondition(input) where input.action == 'addByPhone' AND input.userId IS NOT NULL AND NOT input.hasExistingSpaceMembership_
    - _Expected_Behavior: SpaceMembership created AND space.view permission granted for linked user_
    - _Preservation: Skip if user is null (no linked account) or SpaceMembership already exists_
    - _Requirements: 2.4, 3.1_

  - [x] 3.4 Verify bug condition exploration test now passes (backend)
    - **Property 1: Expected Behavior** - SpaceMembership Created on Group Join
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (SpaceMembership + space.view grant exists after join)
    - When this test passes, it confirms the expected behavior is satisfied for all three handlers
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.4_

  - [x] 3.5 Verify preservation tests still pass (backend)
    - **Property 2: Preservation** - Existing SpaceMembership Idempotency
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2 (idempotent SpaceMembership, no duplicate GroupMembership)
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions in backend behavior)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Fix frontend — Register page redirect preservation

  - [x] 4.1 Preserve redirect query param through registration
    - In `apps/web/app/register/page.tsx`:
    - Read `redirect` param from URL using `useSearchParams()`
    - Change `router.push("/login?registered=1")` to include redirect when present:
    - If redirect exists: `router.push("/login?registered=1&redirect=" + encodeURIComponent(redirect))`
    - If redirect is absent: `router.push("/login?registered=1")` (unchanged)
    - _Bug_Condition: isBugCondition(input) where input.action == 'register' AND input.redirectParam IS NOT NULL_
    - _Expected_Behavior: Post-registration URL is `/login?registered=1&redirect=<original_url>`_
    - _Preservation: Without redirect param, behavior is unchanged (`/login?registered=1`)_
    - _Requirements: 2.2, 3.4, 3.5_

- [x] 5. Fix frontend — API client 401 handling

  - [x] 5.1 Redirect to /login on 401 instead of error page
    - In `apps/web/lib/api/client.ts`, in the 401 response interceptor (refresh failure branch):
    - Replace `redirectToErrorPage("/error/unauthorized")` with:
    - `window.location.href = "/login?redirect=" + encodeURIComponent(window.location.pathname + window.location.search)`
    - Keep existing token cleanup logic (localStorage.removeItem, cookie clearing) unchanged
    - Ensure 403 handling remains unchanged (still redirects to `/error/forbidden`)
    - _Bug_Condition: isBugCondition(input) where input.authStatus == '401_refresh_failed'_ 
    - _Expected_Behavior: Silent redirect to `/login` with current path as redirect param_
    - _Preservation: 403 errors still show `/error/forbidden`, other error codes unaffected_
    - _Requirements: 2.3, 3.3_

- [x] 6. Checkpoint — Ensure all tests pass
  - Run full test suite (backend + frontend)
  - Verify bug condition exploration tests (task 1) now PASS
  - Verify preservation tests (task 2) still PASS
  - Verify no regressions in existing test suite
  - Ensure all tests pass, ask the user if questions arise
