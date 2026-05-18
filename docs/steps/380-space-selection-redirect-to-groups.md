# 380 — Space Selection Redirect to `/groups`

## Phase

Bugfix — Redirect and Member Email Fix

## Purpose

After selecting a space (or being auto-redirected when only one space exists), the app navigated to the obsolete `/schedule/today` route. This step updates both redirect paths to point to `/groups`, the current landing page for multi-group architecture.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/spaces/page.tsx` | Replaced `router.push("/schedule/today")` → `router.push("/groups")` in auto-redirect (single space) and manual `handleSelect` |

## Key decisions

- Minimal surgical fix: only the two `router.push` calls referencing the obsolete route were changed.
- No changes to the `?redirect=` parameter handling — that flow is unaffected and continues to override the default destination.

## How it connects

- Fixes Bug Condition `input.type = "space_selection" AND input.hasNoRedirectParam` from the redirect-and-member-email-fix spec.
- Preserves login redirect with `?redirect=` param (Requirement 3.2) and default post-login redirect to `/schedule/my-missions` (Requirement 3.3).

## How to run / verify

1. Log in with a user that has exactly one space → should auto-redirect to `/groups`.
2. Log in with a user that has multiple spaces → select one → should navigate to `/groups`.
3. Log in with `?redirect=/some-path` → should still redirect to `/some-path` (preservation).

## What comes next

- Task 3.3: Add email field to `GroupMemberDto` and `updatePersonInfo` payload.
- Task 3.6: Re-run bug condition exploration tests to confirm this fix passes.

## Git commit

```bash
git add -A && git commit -m "fix(spaces): redirect to /groups instead of obsolete /schedule/today"
```
