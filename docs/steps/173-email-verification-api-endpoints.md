# 173 — Email Verification API Endpoints

## Phase

Email Verification — API Layer

## Purpose

Expose the email verification and resend functionality via HTTP endpoints in the AuthController, and include the `emailVerified` field in the `/auth/me` response so the frontend can display verification status.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/AuthController.cs` | Added `POST /auth/verify-email` endpoint (`[AllowAnonymous]`), `POST /auth/resend-verification` endpoint (`[Authorize]`), `VerifyEmailRequest` record, and `emailVerified` field to `/auth/me` response |
| `apps/web/middleware.ts` | Added `/verify-email` to `PUBLIC_PATHS` so the page is accessible without auth |

## Key decisions

- `verify-email` is `[AllowAnonymous]` because users click the link from their email and may not be logged in
- `resend-verification` requires `[Authorize]` because only authenticated users should be able to request a resend
- The `resend-verification` endpoint takes no body — the userId is extracted from the JWT claims
- Error handling is delegated to `ExceptionHandlingMiddleware` (InvalidOperationException → 400, KeyNotFoundException → 404)
- Added `/verify-email` to the frontend middleware's PUBLIC_PATHS to prevent redirect-to-login when visiting the verification page

## How it connects

- Dispatches `VerifyEmailCommand` (task 3.1) and `ResendVerificationCommand` (task 4.1) via MediatR
- The `/auth/me` response now includes `emailVerified` which the frontend VerificationBanner (task 10) will consume
- The frontend verify-email page (task 9) will call `POST /auth/verify-email`

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Manual testing:
- `POST /auth/verify-email` with `{ "token": "..." }` → 204 on valid token, 400 on invalid
- `POST /auth/resend-verification` with valid JWT → 204 if unverified, 400 if already verified
- `GET /auth/me` with valid JWT → response includes `emailVerified: false|true`

## What comes next

- Task 8: Frontend API client additions (`verifyEmail`, `resendVerification`, updated `MeDto`)
- Task 9: Frontend verify-email page
- Task 10: Verification banner component

## Git commit

```bash
git add -A && git commit -m "feat(email-verification): add verify-email and resend-verification API endpoints"
```
