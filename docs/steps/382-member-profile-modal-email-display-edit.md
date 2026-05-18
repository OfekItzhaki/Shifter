# 382 — Member Profile Modal Email Display & Edit

## Phase

Bugfix — Redirect and Member Email Fix (Task 3.4)

## Purpose

Add email display and editing capability to the MemberProfileModal component. Previously, the modal showed name, role, and phone but omitted the email field entirely — both in read-only info view and in the edit form.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/useGroupPageState.ts` | Added `email: string` to the `memberEditForm` state type |
| `apps/web/app/groups/[groupId]/page.tsx` | Added `email: selectedMember.email ?? ""` to `onStartEdit` form initialization |
| `apps/web/app/groups/[groupId]/tabs/MembersTab.tsx` | Updated `MemberProfileModalProps` interface to include `email` in `editForm` and `onChangeForm` types; added email display in info view below phone; added email input field in edit form between phone and profile image |

## Key decisions

- Email is displayed in the info view only when non-null (graceful null handling via conditional rendering with `&&`)
- Email input uses `type="email"` for browser-native validation hints
- Email input is placed between phone and profile image in the edit form, matching the design spec
- Uses existing `tProfile("email")` translation key which already exists in en/he/ru locale files

## How it connects

- Depends on task 3.3 which added `email: string | null` to `GroupMemberDto` and `email?: string` to the `updatePersonInfo` payload
- The `handleSaveMemberEdit` handler in `page.tsx` already passes the full `memberEditForm` object to `updatePersonInfo`, so the email field is automatically included in the PUT request
- Task 3.5 (backend) will ensure the API accepts and persists the email field

## How to run / verify

1. Open a group → Members tab → click a member to open the profile modal
2. Verify email is displayed below phone number in the info view (if the member has an email)
3. Click "Edit Details" → verify email input field appears between phone and profile image
4. Change the email → save → verify the updated email appears in the info view

## What comes next

- Task 3.5: Backend `UpdatePersonInfoRequest` must accept `Email` and the `GetGroupMembers` query must project email
- Task 3.6: Re-run bug condition exploration tests to confirm the email display/edit bugs are fixed

## Git commit

```bash
git add -A && git commit -m "fix(members): add email display and edit to MemberProfileModal"
```
