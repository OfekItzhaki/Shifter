# Invitation Flow Fixes — Bugfix Design

## Overview

Multiple related bugs prevent users from successfully joining groups via invitation links. The core issue is that `JoinGroupByCodeCommandHandler`, `AddPersonByEmailCommandHandler`, and `AddPersonByPhoneCommandHandler` create Person + GroupMembership records but fail to create a SpaceMembership, which the PermissionService requires for any space-scoped API call. Secondary issues include the frontend register page losing the redirect URL after registration, and the API client showing an error page instead of silently redirecting to login on 401.

The fix strategy is minimal and targeted: add SpaceMembership + SpaceView permission grant in the three backend handlers, preserve the redirect query parameter through the registration flow, and change the 401 interceptor to redirect to `/login` instead of `/error/unauthorized`.

## Glossary

- **Bug_Condition (C)**: The set of inputs/actions that trigger the bug — joining a group (by code, email, or phone) without an existing SpaceMembership, or registering with a redirect context, or receiving a 401 after refresh failure
- **Property (P)**: The desired behavior — SpaceMembership is created, redirect URL is preserved, silent redirect to login on 401
- **Preservation**: Existing behaviors that must remain unchanged — duplicate membership prevention, direct registration flow, 403 handling, mouse/keyboard interactions
- **SpaceMembership**: Entity in `Jobuler.Domain.Spaces` linking a UserId to a SpaceId, required for PermissionService to grant any access
- **SpacePermissionGrant**: Entity granting a specific permission key to a user within a space
- **PermissionService**: Infrastructure service that checks SpacePermissionGrants (or space ownership) to authorize API calls
- **JoinGroupByCodeCommandHandler**: Handler in `Jobuler.Application.Groups.Commands.JoinCodeCommands.cs` that processes join-by-code requests
- **AddPersonByEmailCommandHandler**: Handler in `Jobuler.Application.Groups.Commands.AddPersonByEmailCommand.cs` that adds a person by email
- **AddPersonByPhoneCommandHandler**: Handler in `Jobuler.Application.Groups.Commands.AddPersonByPhoneCommand.cs` that adds a person by phone

## Bug Details

### Bug Condition

The bug manifests in four scenarios: (1) a user joins a group via code, (2) a user is invited by email, (3) a user is invited by phone — all three create Person + GroupMembership but no SpaceMembership, causing 403 on subsequent API calls; (4) the register page loses the redirect URL, preventing join flow completion after registration; (5) the API client shows an error page on 401 instead of silently redirecting.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type { action, userId, spaceId, hasExistingSpaceMembership, redirectParam, authStatus }
  OUTPUT: boolean
  
  // Backend bug: missing SpaceMembership
  IF input.action IN ['joinByCode', 'addByEmail', 'addByPhone']
     AND input.userId IS NOT NULL
     AND NOT input.hasExistingSpaceMembership
  THEN RETURN TRUE
  
  // Frontend bug: lost redirect
  IF input.action == 'register'
     AND input.redirectParam IS NOT NULL
     AND input.redirectParam != ''
  THEN RETURN TRUE
  
  // Frontend bug: 401 error page
  IF input.authStatus == '401_refresh_failed'
  THEN RETURN TRUE
  
  RETURN FALSE
END FUNCTION
```

### Examples

- **Join by code (Bug 1.1)**: User clicks invite link → lands on `/groups/join?code=ABC123` → authenticates → POST `/groups/join` succeeds → Person + GroupMembership created → user tries GET `/spaces/{id}/groups` → 403 because no SpaceMembership exists
- **Add by email (Bug 1.4)**: Admin adds user@example.com to group → Person + GroupMembership created → invited user logs in → tries to access space → 403
- **Add by phone (Bug 1.4)**: Admin adds +972501234567 to group → same result as email
- **Registration redirect (Bug 1.2)**: User visits `/register?redirect=/groups/join?code=ABC123` → completes registration → redirected to `/login?registered=1` (redirect param lost) → logs in → lands on `/schedule/today` instead of completing join
- **401 error page (Bug 1.3)**: User's token expires → API call returns 401 → refresh fails → user sees `/error/unauthorized` page with a "Login" button instead of being silently redirected to `/login`

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- When a user who already has a SpaceMembership joins a group, no duplicate SpaceMembership is created (idempotent check)
- When a user is already a member of a group and joins again, success is returned without duplicate memberships
- When a 403 (Forbidden) occurs due to insufficient permissions within a space the user belongs to, the forbidden error page continues to display
- When a user registers without an invitation context (no redirect param), the flow continues to redirect to `/login?registered=1` → `/schedule/today`
- When a user logs in with a redirect parameter, the login page continues to redirect to the specified URL
- Phone-only registration continues to work with placeholder email
- WhatsApp invitations via Twilio continue to function
- The `AddPersonToGroupByIdCommand` (adding existing space persons) continues to work without changes since those persons already have SpaceMembership
- Space owner implicit permission bypass in PermissionService remains unchanged

**Scope:**
All inputs that do NOT involve the bug conditions should be completely unaffected by this fix. This includes:
- Normal space-scoped API calls by users who already have SpaceMembership
- Direct registration without redirect context
- 403 errors from legitimate permission denials
- 404 and 5xx error handling in the API client

## Hypothesized Root Cause

Based on code analysis, the root causes are confirmed:

1. **Missing SpaceMembership in JoinGroupByCodeCommandHandler** (`JoinCodeCommands.cs` lines 63-95): The handler creates a Person and GroupMembership but never checks for or creates a SpaceMembership. It also does not grant the `space.view` permission. Since PermissionService checks `SpacePermissionGrants` (not GroupMembership), the user gets 403 on all subsequent space-scoped calls.

2. **Missing SpaceMembership in AddPersonByEmailCommandHandler** (`AddPersonByEmailCommand.cs`): Same pattern — creates Person + GroupMembership + invitation but no SpaceMembership or permission grant for the linked user.

3. **Missing SpaceMembership in AddPersonByPhoneCommandHandler** (`AddPersonByPhoneCommand.cs`): Identical issue to the email handler.

4. **Register page hardcodes redirect** (`apps/web/app/register/page.tsx` line 56): After successful registration, `router.push("/login?registered=1")` is called without preserving the `redirect` search param that was passed from the join page.

5. **API client redirects to error page** (`apps/web/lib/api/client.ts` line 57): When refresh fails, `redirectToErrorPage("/error/unauthorized")` is called, showing a dedicated error page instead of silently redirecting to `/login`.

## Correctness Properties

Property 1: Bug Condition - SpaceMembership Created on Group Join

_For any_ input where a user joins a group (by code, email invitation, or phone invitation) and does NOT already have a SpaceMembership in the group's space, the fixed handlers SHALL create a SpaceMembership record and grant the `space.view` permission, enabling the user to make space-scoped API calls.

**Validates: Requirements 2.1, 2.4**

Property 2: Bug Condition - Registration Redirect Preserved

_For any_ registration attempt where a `redirect` query parameter is present, the fixed register page SHALL include the redirect parameter in the post-registration navigation to the login page, formatted as `/login?registered=1&redirect=<original_url>`.

**Validates: Requirements 2.2**

Property 3: Bug Condition - Silent Redirect on 401

_For any_ 401 response where the token refresh fails, the fixed API client SHALL redirect the user to `/login` (preserving the current path as a redirect param) instead of showing the `/error/unauthorized` error page.

**Validates: Requirements 2.3**

Property 4: Preservation - No Duplicate SpaceMembership

_For any_ input where a user joins a group and ALREADY has an active SpaceMembership in the group's space, the fixed handlers SHALL skip SpaceMembership creation and produce the same result as the original function, preserving idempotent behavior.

**Validates: Requirements 3.1, 3.2**

Property 5: Preservation - Direct Registration Flow

_For any_ registration attempt where NO `redirect` query parameter is present, the fixed register page SHALL redirect to `/login?registered=1` exactly as before, preserving the default registration flow.

**Validates: Requirements 3.4**

Property 6: Preservation - 403 Error Page Unchanged

_For any_ 403 response from the API, the fixed client SHALL continue to redirect to `/error/forbidden`, preserving the existing forbidden error handling.

**Validates: Requirements 3.3**

## Fix Implementation

### Changes Required

**File**: `apps/api/Jobuler.Application/Groups/Commands/JoinCodeCommands.cs`

**Function**: `JoinGroupByCodeCommandHandler.Handle`

**Specific Changes**:
1. **Add SpaceMembership check and creation**: After creating/finding the Person and before/after creating GroupMembership, check if a SpaceMembership exists for `req.UserId` in `group.SpaceId`. If not, create one via `SpaceMembership.Create(group.SpaceId, req.UserId)`.
2. **Grant SpaceView permission**: If SpaceMembership was just created, also add a `SpacePermissionGrant.Grant(group.SpaceId, req.UserId, Permissions.SpaceView, req.UserId)` so the user can access the space.

---

**File**: `apps/api/Jobuler.Application/Groups/Commands/AddPersonByEmailCommand.cs`

**Function**: `AddPersonByEmailCommandHandler.Handle`

**Specific Changes**:
1. **Add SpaceMembership for linked user**: After step 4 (adding to group), if `user is not null` (i.e., the invited person has an account), check if a SpaceMembership exists for `user.Id` in `req.SpaceId`. If not, create one.
2. **Grant SpaceView permission**: Same as above — grant `space.view` to the linked user.

---

**File**: `apps/api/Jobuler.Application/Groups/Commands/AddPersonByPhoneCommand.cs`

**Function**: `AddPersonByPhoneCommandHandler.Handle`

**Specific Changes**:
1. **Add SpaceMembership for linked user**: Same pattern as email handler — if `user is not null`, ensure SpaceMembership exists.
2. **Grant SpaceView permission**: Grant `space.view` to the linked user.

---

**File**: `apps/web/app/register/page.tsx`

**Function**: `handleSubmit`

**Specific Changes**:
1. **Read redirect param from URL**: Use `useSearchParams()` to get the `redirect` query parameter.
2. **Preserve redirect in post-registration navigation**: Change `router.push("/login?registered=1")` to `router.push("/login?registered=1&redirect=" + encodeURIComponent(redirect))` when redirect is present, or keep the original path when it's absent.

---

**File**: `apps/web/lib/api/client.ts`

**Function**: Response interceptor (401 catch block)

**Specific Changes**:
1. **Change redirect target**: Replace `redirectToErrorPage("/error/unauthorized")` with a redirect to `/login` that includes the current path as a redirect parameter: `window.location.href = "/login?redirect=" + encodeURIComponent(window.location.pathname + window.location.search)`.
2. **Keep token cleanup**: The `localStorage.removeItem` and cookie clearing logic remains unchanged.

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write integration tests that simulate the join-by-code, add-by-email, and add-by-phone flows, then assert that a SpaceMembership exists after the operation. Run these tests on the UNFIXED code to observe failures.

**Test Cases**:
1. **Join by Code — No SpaceMembership**: Call `JoinGroupByCodeCommandHandler` with a valid user and code → assert `SpaceMemberships` table has a record for the user in the group's space (will fail on unfixed code)
2. **Add by Email — No SpaceMembership**: Call `AddPersonByEmailCommandHandler` with a valid email linked to an existing user → assert SpaceMembership exists (will fail on unfixed code)
3. **Add by Phone — No SpaceMembership**: Call `AddPersonByPhoneCommandHandler` with a valid phone linked to an existing user → assert SpaceMembership exists (will fail on unfixed code)
4. **Register with Redirect**: Simulate registration with `?redirect=/groups/join?code=ABC` → assert the post-registration URL contains the redirect param (will fail on unfixed code)

**Expected Counterexamples**:
- After `JoinGroupByCodeCommandHandler` completes, `SpaceMemberships.Any(sm => sm.UserId == userId && sm.SpaceId == spaceId)` returns false
- After `AddPersonByEmailCommandHandler` completes with a linked user, same assertion fails
- The register page navigates to `/login?registered=1` without the redirect parameter

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := fixedHandler(input)
  ASSERT SpaceMemberships.Any(sm => sm.UserId == input.userId && sm.SpaceId == input.spaceId)
  ASSERT SpacePermissionGrants.Any(g => g.UserId == input.userId && g.SpaceId == input.spaceId && g.PermissionKey == "space.view")
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT fixedHandler(input) == originalHandler(input)
  // Specifically:
  // - No duplicate SpaceMembership created
  // - No duplicate permission grants
  // - GroupMembership behavior unchanged
  // - Person creation behavior unchanged
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain (various combinations of existing/missing memberships)
- It catches edge cases that manual unit tests might miss (e.g., user with deactivated SpaceMembership)
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for users who already have SpaceMembership, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Existing SpaceMembership Preservation**: User already has SpaceMembership → join by code → assert no duplicate created, membership count unchanged
2. **Existing GroupMembership Preservation**: User already in group → join again → assert idempotent success, no duplicates
3. **Direct Registration Preservation**: Register without redirect param → assert navigation to `/login?registered=1` (no redirect appended)
4. **403 Handling Preservation**: Simulate 403 response → assert redirect to `/error/forbidden` (not affected by 401 fix)

### Unit Tests

- Test `JoinGroupByCodeCommandHandler` creates SpaceMembership + SpaceView grant for new user
- Test `JoinGroupByCodeCommandHandler` skips SpaceMembership when one already exists
- Test `AddPersonByEmailCommandHandler` creates SpaceMembership for linked user
- Test `AddPersonByEmailCommandHandler` does NOT create SpaceMembership when user is null (no linked account)
- Test `AddPersonByPhoneCommandHandler` creates SpaceMembership for linked user
- Test register page preserves redirect param through registration
- Test register page works without redirect param (default behavior)
- Test API client redirects to `/login` on 401 refresh failure

### Property-Based Tests

- Generate random user/space/group combinations and verify: if user has no SpaceMembership before join, one exists after; if user already has one, count stays the same
- Generate random registration scenarios with/without redirect params and verify correct navigation target
- Generate random API error responses and verify correct redirect behavior (401 → login, 403 → forbidden, others unchanged)

### Integration Tests

- Full end-to-end: create space → create group → generate join code → new user joins → verify user can call space-scoped APIs
- Full end-to-end: admin adds user by email → invited user logs in → verify user can access space
- Full end-to-end: unauthenticated user visits join link → registers → logs in → completes join → can access group
- Verify that after fix, the PermissionService grants access for joined users
