# 381 — Add email field to GroupMemberDto and updatePersonInfo payload

## Phase

Bugfix — Redirect and Member Email Fix (Task 3.3)

## Purpose

The member details modal cannot display or edit email addresses because the `GroupMemberDto` interface lacks an `email` field and the `updatePersonInfo` function's payload type does not include it. This step adds the email field to both types so the frontend can receive and send email data.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/api/groups.ts` | Added `email: string \| null` to `GroupMemberDto` interface |
| `apps/web/lib/api/groups.ts` | Added `email?: string` to `updatePersonInfo` payload type |

## Key decisions

- `email` in `GroupMemberDto` is `string | null` because not all members have an email address (matches the backend nullable column).
- `email` in the `updatePersonInfo` payload is optional (`email?: string`) so existing callers that don't pass email continue to work without modification.

## How it connects

- **Downstream**: Task 3.4 (MemberProfileModal UI) will read `member.email` from the DTO and include it in the edit form payload.
- **Downstream**: Task 3.5 (backend) will project the email in the GetGroupMembers query and accept it in UpdatePersonInfoRequest.
- **Preservation**: All existing fields remain unchanged — no breaking changes to consumers of `GroupMemberDto` or `updatePersonInfo`.

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

No type errors should appear. The new field is additive and optional in the payload.

## What comes next

- Task 3.4: Add email display and edit UI to MemberProfileModal
- Task 3.5: Backend changes to project and accept email

## Git commit

```bash
git add -A && git commit -m "fix(web): add email field to GroupMemberDto and updatePersonInfo payload"
```
