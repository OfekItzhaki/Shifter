# Step 231 — Hebrew Error Messages + "What's New" Inside Group

## Phase

Phase 5 — Polish & Production Readiness

## Purpose

Fix two production issues:
1. Backend API error messages were in English — users see raw English text in error toasts
2. "What's New" (changelog) was only accessible from global nav — users wanted it inside the group view

## What was built

### Issue 1: Hebrew error messages throughout the API

- **`apps/api/Jobuler.Api/Middleware/ExceptionHandlingMiddleware.cs`** — Translated all generic error messages: "Validation failed" → "אימות הנתונים נכשל", "You do not have permission" → "אין לך הרשאה לבצע פעולה זו", "A record with this name already exists" → "רשומה עם שם או מזהה זה כבר קיימת", DB errors, check constraint messages, etc.
- **`apps/api/Jobuler.Infrastructure/Auth/PermissionService.cs`** — Permission denied message now in Hebrew.
- **`apps/api/Jobuler.Infrastructure/Auth/Fido2Service.cs`** — WebAuthn challenge/attestation errors in Hebrew.
- **`apps/api/Jobuler.Domain/Groups/Group.cs`** — Group name validation in Hebrew.
- **`apps/api/Jobuler.Domain/Groups/HomeLeaveConfig.cs`** — Home-leave config validation messages in Hebrew.
- **`apps/api/Jobuler.Domain/Groups/HomeLeaveTemplate.cs`** — Template name validation in Hebrew.
- **`apps/api/Jobuler.Domain/Scheduling/ScheduleVersion.cs`** — Publish/discard validation in Hebrew.
- **`apps/api/Jobuler.Domain/Spaces/SpaceRole.cs`** — Default role deletion error in Hebrew.
- **`apps/api/Jobuler.Domain/Spaces/UnavailabilityReason.cs`** — Display name validation in Hebrew.
- **`apps/api/Jobuler.Domain/Identity/WebAuthnCredential.cs`** — Credential validation in Hebrew.
- **`apps/api/Jobuler.Application/Tasks/Commands/GroupTaskCommands.cs`** — Task/group not found errors in Hebrew.
- **`apps/api/Jobuler.Application/Tasks/Queries/GetGroupTasksQuery.cs`** — Group not found error in Hebrew.
- **`apps/api/Jobuler.Api/Controllers/ScheduleRunsController.cs`** — Invalid trigger mode error in Hebrew.

### Issue 2: "What's New" link inside group page

- **`apps/web/app/groups/[groupId]/page.tsx`** — Added a purple "מה חדש" badge/link in the group header that navigates to `/changelog`.
- **`apps/web/messages/he.json`** — Added `groups.whatsNew` key: "מה חדש".
- **`apps/web/messages/en.json`** — Added `groups.whatsNew` key: "What's New".

## Key decisions

- Error messages are hardcoded in Hebrew rather than using a localization framework on the backend — the app is Hebrew-first and the backend doesn't have i18n infrastructure. The solver notifications already use locale-based messages (they check `space.Locale`), but for error messages the overhead isn't justified since 99% of users are Hebrew speakers.
- "What's New" is a simple link rather than a full tab — it navigates to the existing `/changelog` page. This keeps the group page clean while making the feature discoverable.
- Internal/infrastructure errors (config missing, Redis unavailable) remain in English since they're only seen in logs, not by end users.

## How it connects

- The `ExceptionHandlingMiddleware` is the single point where all API errors are formatted for the client.
- The solver worker already had locale-aware notifications — this change makes the rest of the API consistent.
- The "What's New" link uses the existing `Link` component from Next.js and the existing `/changelog` page.

## How to run / verify

1. Trigger a validation error (e.g., create a task with end time before start time). Verify the error message is in Hebrew.
2. Try to access a resource without permission. Verify "אין לך הרשאה" message.
3. Navigate to a group page. Verify the purple "מה חדש" badge appears in the header.
4. Click it — should navigate to the changelog page.

## What comes next

- Home-leave slider feature with solver preview (new spec)
- Consider adding a "new" badge that disappears after the user views the changelog

## Git commit

```bash
git add -A && git commit -m "fix(phase5): hebrew error messages throughout API, what's-new link inside group"
```
