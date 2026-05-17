# 311 — Re-Authenticate API Endpoint

## Phase

Admin Session Timeout — API Controller Endpoints

## Purpose

Exposes the `POST /auth/re-authenticate` endpoint so the frontend can verify a user's identity before entering elevated privilege modes (Management Mode or Super Platform Mode). This endpoint dispatches the existing `ReAuthenticateCommand` and returns a uniform success/failure response without leaking credential failure details.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/AuthController.cs` | Added `ReAuthenticate` action method and `ReAuthenticateRequest` record |

### Endpoint details

- **Route:** `POST /auth/re-authenticate`
- **Auth:** `[Authorize]` (inherits `[EnableRateLimiting("auth")]` from controller)
- **Request body:** `ReAuthenticateRequest(Password?, WebAuthnChallengeId?, WebAuthnAssertionJson?, SpaceId?)`
- **Success:** `200 { success: true }`
- **Failure:** `401 { error: "Authentication failed." }`

## Key decisions

- Reuses the controller-level `[EnableRateLimiting("auth")]` attribute — no additional rate limiting annotation needed on the action.
- Extracts IP from `HttpContext.Connection.RemoteIpAddress` for audit logging, consistent with other auth operations.
- Returns a generic 401 error message on failure to prevent user enumeration (security requirement 9.2).

## How it connects

- Dispatches `ReAuthenticateCommand` (created in step 308) which handles BCrypt/WebAuthn verification and audit logging.
- Will be called by the frontend `ReAuthDialog` component before entering Management Mode or Super Platform Mode.
- Rate limiting is enforced by the existing "auth" fixed-window policy (10 req/min/IP in production).

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with zero errors.

## What comes next

- Task 4.2: `POST /auth/session-timeout-event` endpoint
- Task 4.3: Extend group settings PATCH to include `managementTimeoutMinutes`
- Task 4.4: Platform settings GET/PATCH endpoints

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): add POST /auth/re-authenticate endpoint"
```
