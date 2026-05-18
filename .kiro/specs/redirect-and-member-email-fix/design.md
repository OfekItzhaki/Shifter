# Redirect and Member Email Fix — Bugfix Design

## Overview

This bugfix addresses three navigation and data-display issues in the Shifter web application:

1. **Pricing page back link** hardcodes `/login` instead of using browser history navigation.
2. **Post-login/space-selection redirect** sends users to the obsolete `/schedule/today` route instead of `/groups`.
3. **Member details modal** omits the email field entirely — it is neither displayed in read-only mode nor editable in the edit form.

The fix is minimal and surgical: each issue is a single-point change in the frontend with one small backend/DTO addition for the email field.

## Glossary

- **Bug_Condition (C)**: The set of user interactions that trigger incorrect navigation or missing data display
- **Property (P)**: The desired correct behavior — proper navigation targets and email visibility
- **Preservation**: Existing behaviors (mouse clicks, other redirects, non-email field editing) that must remain unchanged
- **`/schedule/today`**: An obsolete route from the old single-group schedule view; no longer valid
- **`/groups`**: The current "my groups" landing page that should be the post-space-selection destination
- **`GroupMemberDto`**: The TypeScript interface in `lib/api/groups.ts` representing a group member's data
- **`MemberProfileModal`**: The component in `MembersTab.tsx` that displays and edits member details
- **`updatePersonInfo`**: The API function that PUTs member field updates to `/spaces/{id}/people/{id}/info`

## Bug Details

### Bug Condition

The bug manifests in three independent scenarios:

1. A user clicks the "back" link on the pricing page — the system navigates to `/login` regardless of context.
2. A user selects a space (or is auto-redirected with a single space) — the system navigates to `/schedule/today`.
3. An admin opens the member details modal — the email address is absent from both display and edit views.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type UserInteraction
  OUTPUT: boolean

  CASE input.type OF
    "pricing_back_click":
      RETURN true  -- always triggers incorrect /login navigation

    "space_selection":
      RETURN input.hasNoRedirectParam
             -- when no ?redirect= param, system goes to /schedule/today

    "member_modal_open":
      RETURN true  -- email is never shown regardless of member data

    "member_edit_start":
      RETURN true  -- email field is never present in edit form
  END CASE

  RETURN false
END FUNCTION
```

### Examples

- **Pricing back link**: User is authenticated, browsing `/pricing` from `/groups`. Clicks "← Back". Expected: returns to `/groups` (browser history). Actual: navigates to `/login`.
- **Space selection redirect**: User logs in, has one space, auto-redirected. Expected: lands on `/groups`. Actual: lands on `/schedule/today` (404 or empty page).
- **Member email display**: Admin opens member "Alice" who has email `alice@example.com`. Expected: email shown in info section. Actual: only name, role, and phone are displayed.
- **Member email edit**: Admin clicks "Edit Details" for member "Bob". Expected: email field appears alongside name/phone/birthday. Actual: no email field in the form.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- Mouse clicks on all other navigation links must continue to work exactly as before
- The `?redirect=` query parameter on the login page must continue to override the default post-login destination
- The default post-login redirect (without `?redirect=`) must remain `/schedule/my-missions`
- Editing a member's name, phone, display name, birthday, and profile image must continue to save correctly
- Non-admin users viewing the member modal must continue to see read-only info without edit controls
- The group owner's ability to edit admin members must remain unchanged
- The pricing page itself must continue to render without requiring authentication

**Scope:**
All inputs that do NOT involve the three bug conditions should be completely unaffected by this fix. This includes:
- All other navigation links and router.push calls throughout the app
- Login flow with explicit `?redirect=` parameter
- Member modal availability tab functionality
- Add-member modal (which already has an email field)

## Hypothesized Root Cause

Based on the code analysis, the root causes are:

1. **Pricing page back link — hardcoded href**: In `apps/web/app/pricing/page.tsx`, line ~107, the back link uses `<Link href="/login">` instead of calling `router.back()` or using `window.history.back()`. This was likely a leftover from when the pricing page was only accessible from the login/register flow.

2. **Space selection redirect — obsolete route**: In `apps/web/app/spaces/page.tsx`, lines 37 and 50, `router.push("/schedule/today")` references a route that no longer exists. The correct destination is `/groups`. This was not updated when the app transitioned from a single-group to multi-group architecture.

3. **Member email — missing from DTO and UI**:
   - The `GroupMemberDto` interface in `lib/api/groups.ts` does not include an `email` field.
   - The backend `GetGroupMembers` query likely does not project the email from the `People` table.
   - The `MemberProfileModal` component has no email display in the info view.
   - The `editForm` state shape (`{ fullName, displayName, phoneNumber, profileImageUrl, birthday }`) omits email.
   - The `UpdatePersonInfoRequest` record in `PeopleController.cs` does not accept an `Email` property.
   - The `updatePersonInfo` API function payload type does not include `email`.

## Correctness Properties

Property 1: Bug Condition - Navigation and Display Corrections

_For any_ user interaction where the bug condition holds (isBugCondition returns true), the fixed application SHALL navigate to the correct destination (browser history for pricing back, `/groups` for space selection) and SHALL display/allow editing of the member email address in the member details modal.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation - Existing Navigation and Edit Behavior

_For any_ user interaction where the bug condition does NOT hold (isBugCondition returns false), the fixed application SHALL produce exactly the same behavior as the original application, preserving all existing navigation flows, redirect parameter handling, field editing, and permission enforcement.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `apps/web/app/pricing/page.tsx`

**Change 1 — Replace hardcoded Link with history navigation**:
- Remove the `<Link href="/login">` back link
- Replace with a `<button>` that calls `router.back()` (using Next.js `useRouter`)
- Add a fallback: if `window.history.length <= 1`, navigate to `/` instead

**File**: `apps/web/app/spaces/page.tsx`

**Change 2 — Update redirect target**:
- Replace both instances of `router.push("/schedule/today")` with `router.push("/groups")`
- Line 37 (auto-redirect for single space) and line 50 (manual space selection)

**File**: `apps/web/lib/api/groups.ts`

**Change 3 — Add email to GroupMemberDto**:
- Add `email: string | null;` to the `GroupMemberDto` interface
- Add `email?: string` to the `updatePersonInfo` payload type

**File**: `apps/web/app/groups/[groupId]/tabs/MembersTab.tsx`

**Change 4 — Add email to modal display and edit form**:
- In the `MemberProfileModalProps` interface, update the `editForm` type to include `email: string`
- In the info view (read-only), display `member.email` below the phone number
- In the edit form, add an email input field between phone and profile image
- The `onChangeForm` type must be updated to include `email`

**File**: `apps/web/app/groups/[groupId]/page.tsx`

**Change 5 — Initialize email in edit form state**:
- Update `onStartEdit` to include `email: selectedMember.email ?? ""` in the form initialization

**File**: `apps/web/app/groups/[groupId]/useGroupPageState.ts`

**Change 6 — Add email to form state type**:
- Add `email: string` to the `memberEditForm` state type

**File**: `apps/api/Jobuler.Api/Controllers/PeopleController.cs`

**Change 7 — Add Email to UpdatePersonInfoRequest**:
- Add `string? Email` to the `UpdatePersonInfoRequest` record
- Pass it through to the `UpdatePersonInfoCommand`

**File**: Backend query for group members (likely in Application/Groups or Infrastructure)

**Change 8 — Project email in GetGroupMembers query**:
- Include the person's email in the group members query result so it appears in the API response

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bugs on unfixed code, then verify the fixes work correctly and preserve existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bugs BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write unit tests that exercise the navigation logic and modal rendering. Run these tests on the UNFIXED code to observe failures and confirm root causes.

**Test Cases**:
1. **Pricing Back Link Test**: Assert that clicking the back element navigates to browser history, not `/login` (will fail on unfixed code — currently renders `<Link href="/login">`)
2. **Space Selection Redirect Test**: Assert that `handleSelect` calls `router.push("/groups")` (will fail on unfixed code — currently pushes `/schedule/today`)
3. **Member Email Display Test**: Assert that the member modal info view renders the email address (will fail on unfixed code — no email rendering exists)
4. **Member Email Edit Test**: Assert that the edit form contains an email input field (will fail on unfixed code — no email field in form)

**Expected Counterexamples**:
- Pricing back link resolves to `/login` instead of calling history.back()
- Space selection pushes `/schedule/today` instead of `/groups`
- Member modal does not render any email-related DOM elements
- Possible causes confirmed: hardcoded href, obsolete route string, missing DTO field and UI elements

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed functions produce the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := executeFixedInteraction(input)
  ASSERT expectedBehavior(result)
END FOR
```

Specifically:
- For pricing back: assert navigation target is `history.back()` or `/` as fallback
- For space selection: assert navigation target is `/groups`
- For member email display: assert email is rendered when present in DTO
- For member email edit: assert email field exists and value is sent in PUT payload

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed code produces the same result as the original code.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT originalBehavior(input) = fixedBehavior(input)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain
- It catches edge cases that manual unit tests might miss
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for non-bug interactions, then write property-based tests capturing that behavior.

**Test Cases**:
1. **Login Redirect Preservation**: Verify that login with `?redirect=/some-path` still redirects to `/some-path` after fix
2. **Default Login Redirect Preservation**: Verify that login without `?redirect=` still goes to `/schedule/my-missions`
3. **Member Field Edit Preservation**: Verify that editing name, phone, birthday, displayName, profileImage continues to save correctly
4. **Permission Preservation**: Verify that non-admin users cannot see edit controls, and non-owners cannot edit admin members

### Unit Tests

- Test pricing page renders a button/link that triggers `router.back()` instead of navigating to `/login`
- Test spaces page `handleSelect` calls `router.push("/groups")`
- Test spaces page auto-redirect (single space) calls `router.push("/groups")`
- Test `GroupMemberDto` interface includes `email` field
- Test member modal info view renders email when present
- Test member modal info view handles null email gracefully
- Test member edit form includes email input
- Test `updatePersonInfo` payload includes email field

### Property-Based Tests

- Generate random member data (with/without email) and verify the modal correctly displays email when present and omits it when null
- Generate random navigation scenarios and verify only the three bug-condition paths are changed
- Generate random edit form payloads and verify all fields (including email) are correctly passed to the API function

### Integration Tests

- Test full flow: navigate to pricing → click back → verify correct destination
- Test full flow: login → space selection → verify landing on `/groups`
- Test full flow: open member modal → verify email displayed → edit → change email → save → verify API call includes email
- Test permission enforcement: non-admin opens modal → no edit button → no email edit possible
- Test admin-member restriction: admin opens another admin's modal → edit disabled (owner-only)
