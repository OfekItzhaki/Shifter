# Step 055 — Invitation Accept Flow: End-to-End Fixes

## Phase
Phase 8 — Production Hardening

## Purpose
The invitation accept flow had three issues preventing a clean end-to-end test:
1. The invite URL was hardcoded to `https://jobuler.app` — broken in dev/staging
2. The login page always redirected to `/schedule/today` after login, losing the invitation token from the URL
3. The accept handler loaded the `Person` without verifying it belonged to the invitation's space

## What was built

### `apps/api/Jobuler.Api/appsettings.json`
Added `App:FrontendBaseUrl` config key (production default: `https://jobuler.app`).

### `apps/api/Jobuler.Api/appsettings.Development.json`
Added `App:FrontendBaseUrl` set to `http://localhost:3000` for local dev.

### `apps/api/Jobuler.Application/People/Commands/InvitePersonCommand.cs`
- Injected `IConfiguration` into the handler
- Reads `App:FrontendBaseUrl` from config instead of hardcoding the URL
- Falls back to `https://jobuler.app` if config key is missing

### `apps/api/Jobuler.Application/People/Commands/AcceptInvitationCommand.cs`
- Added `&& p.SpaceId == invitation.SpaceId` to the `Person` lookup query
- Prevents a token from linking a user to a person in a different space

### `apps/web/app/login/page.tsx`
- Reads `?redirect=` query param and redirects there after successful login (falls back to `/schedule/today`)
- Wrapped `useSearchParams()` usage in a `LoginForm` inner component with a `Suspense` boundary (required by Next.js App Router)

## Key decisions
- Config-driven base URL rather than env var on the frontend — keeps the invite URL generation server-side and testable
- `Suspense` wrapper is minimal (no loading UI) since the login page renders fast and the param is only needed after mount

## How it connects
- `InvitePersonCommand` → builds invite URL → `IInvitationSender` → user clicks link → `/invitations/accept?token=...`
- If not logged in → `/login?redirect=/invitations/accept?token=...` → after login → back to accept page → `POST /invitations/accept?token=...` → person linked

## How to run / verify
1. Start the API and frontend
2. Create a person in a space
3. Call `POST /spaces/{spaceId}/people/{personId}/invite` with `{ "contact": "test@example.com", "channel": "email" }`
4. Check API console logs for the invite URL (NoOpInvitationSender logs it)
5. Open the URL in a browser — if not logged in, you'll be redirected to login with the token preserved
6. After login, the accept page fires automatically and links the user to the person
7. Verify in DB: `SELECT linked_user_id, invitation_status FROM people WHERE id = '{personId}'`

## What comes next
- Wire a real email or WhatsApp sender (replace `NoOpInvitationSender`)
- Add audit log entry on invitation accept

## Git commit
```bash
git add -A && git commit -m "fix(invitations): config-driven invite URL, redirect-after-login, space-scoped person lookup"
```
