# Step 108 — Auth Language Switcher, Register Image Upload, Stale Schedule Guard

## Phase
Phase 9 — Polish & Hardening

## Purpose
Three independent improvements:
1. Language selection was only available inside the app shell (sidebar). Users couldn't change language before logging in.
2. The register page had a plain URL text input for the profile image — inconsistent with the profile edit modal which uses the full `ImageUpload` component (file upload + URL paste + sanitization).
3. The scheduler could be triggered against a group whose tasks all ended in the past, producing a meaningless empty draft and wasting solver capacity.

## What was built

### `apps/web/components/LanguageSwitcher.tsx` (new)
Extracted the language switcher from `AppShell` into a standalone component. Accepts a `variant` prop:
- `"sidebar"` (default) — original dark sidebar style, used in `AppShell`
- `"auth"` — light card style with neutral borders, suitable for the white auth pages

### `apps/web/components/shell/AppShell.tsx` (modified)
- Removed the inline `LanguageSwitcher` function and `LOCALES` constant
- Removed the now-unused `useLocale` import
- Imports and uses `<LanguageSwitcher />` (sidebar variant) — identical visual output

### `apps/web/app/login/page.tsx` (modified)
- Added `<LanguageSwitcher variant="auth" />` between the logo and the card

### `apps/web/app/register/page.tsx` (modified)
- Added `<LanguageSwitcher variant="auth" />` between the logo and the card
- Replaced the plain `<input type="url">` for profile image with `<ImageUpload shape="circle" size={80} />` — users can now upload a file or paste a URL, with client-side sanitization (https-only, no localhost in production)

### `apps/web/app/forgot-password/page.tsx` (modified)
- Added `<LanguageSwitcher variant="auth" />` between the logo and the card

### `apps/api/Jobuler.Application/Scheduling/Commands/TriggerSolverCommand.cs` (modified)
Added a stale-task guard before the solver run is enqueued. When a `GroupId` is provided:
- Queries `GroupTasks` for any active task with `EndsAt > nowUtc`
- If none exist, throws `InvalidOperationException` with a descriptive message
- The existing `ExceptionHandlingMiddleware` maps `InvalidOperationException` → HTTP 400, so the frontend receives a clear error
- The `nowUtc` respects the optional `StartTime` override (same logic as the normalizer)

## Key decisions
- **Stale guard is group-scoped only**: Space-wide runs (no `GroupId`) are not blocked — they may include groups with future tasks. The guard only fires when the admin explicitly targets a specific group.
- **`InvalidOperationException` → 400**: Consistent with the existing error handling convention. No new exception type needed.
- **`ImageUpload` reuse**: The component already handles file size limits (10 MB), MIME type filtering (jpeg/png/webp/gif), URL sanitization (https-only), and upload via the existing `/uploads` endpoint. No new logic needed.
- **Qualification requirements**: Already fully implemented — `GroupTask.RequiredQualificationNames`, the API, the solver payload, and the `TasksTab` UI all support it. No changes needed.

## How it connects
- `LanguageSwitcher` is now a shared component used by both `AppShell` and the three auth pages
- The stale-task guard fires in the Application layer before the job is enqueued, keeping the solver worker clean
- `ImageUpload` on register uses the same `/api/uploads` endpoint as the profile edit modal

## How to run / verify
1. Open `/login`, `/register`, `/forgot-password` — language buttons appear below the logo
2. Switch language on the login page — page reloads in the selected language
3. On `/register`, the profile image field shows the upload widget (click to upload file, or "Enter URL" to paste a link)
4. Create a group task with `EndsAt` in the past, then trigger the scheduler for that group — expect HTTP 400 with message "Cannot create a schedule: all tasks for this group end in the past."
5. Update the task to a future date and trigger again — scheduler runs normally

## What comes next
- Continue with the schedule-table-autoschedule-role-constraints spec tasks

## Git commit

```bash
git add -A && git commit -m "feat(ux): auth language switcher, register image upload, stale schedule guard"
```
