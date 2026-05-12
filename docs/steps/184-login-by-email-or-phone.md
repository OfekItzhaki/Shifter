# 184 — Login by Email or Phone Number

## Phase
Phase 8 — UX Improvements

## Purpose
Allow users to log in using either their email address or phone number. Also make registration require only one of email/phone (not both), enabling phone-only registration flows.

## What was built

### Backend
- **`Jobuler.Application/Auth/Commands/LoginCommand.cs`** — Changed `Email` property to `Identifier` to accept either email or phone
- **`Jobuler.Application/Auth/Commands/LoginCommandHandler.cs`** — Updated handler to detect whether identifier is email (contains `@`) or phone, and look up user accordingly. Added `NormalizePhone` helper.
- **`Jobuler.Application/Auth/Validators/LoginCommandValidator.cs`** — Replaced email-specific validation with generic identifier validation (not empty, max 256 chars)
- **`Jobuler.Application/Auth/Validators/RegisterCommandValidator.cs`** — Made email optional; added rule requiring at least one of email or phone
- **`Jobuler.Application/Auth/Commands/RegisterCommandHandler.cs`** — Added conditional uniqueness checks for email/phone, placeholder email generation for phone-only registrations, and conditional verification email sending
- **`Jobuler.Api/Controllers/AuthController.cs`** — Updated `LoginRequest` to support both `email` (legacy) and `identifier` (new) fields with `ResolvedIdentifier` property. Made `RegisterRequest.Email` nullable.

### Frontend
- **`apps/web/lib/api/auth.ts`** — Changed `login()` to send `identifier` instead of `email`. Changed `register()` parameter order to make email optional.
- **`apps/web/lib/store/authStore.ts`** — Updated login function signature from `email` to `identifier`
- **`apps/web/app/login/page.tsx`** — Changed input type from `email` to `text`, updated label to use `emailOrPhone` i18n key, updated placeholder
- **`apps/web/app/register/page.tsx`** — Made email field optional, added validation requiring at least email or phone, updated register API call
- **`apps/web/messages/en.json`** — Added `emailOrPhone`, `emailOrPhoneRequired`, `emailOrPhoneHint` keys
- **`apps/web/messages/he.json`** — Added Hebrew translations for new keys
- **`apps/web/messages/ru.json`** — Added Russian translations for new keys

## Key decisions
- **Backward compatibility**: The `LoginRequest` accepts both `email` (legacy) and `identifier` (new) fields. Existing clients sending `{ "email": "..." }` still work.
- **Phone detection**: Uses `@` presence to distinguish email from phone — simple and reliable.
- **Placeholder email**: Phone-only registrations get a `phone_<digits>@phone.local` placeholder email to satisfy the domain model's email requirement.
- **Conditional verification**: Verification emails are only sent when a real email is provided.
- **Phone normalization**: Strips spaces, dashes, and parentheses for phone lookup.

## How it connects
- Builds on the existing JWT auth system (step 004)
- Uses the existing FluentValidation pipeline
- The `User.Create()` domain method still receives an email string (placeholder for phone-only users)

## How to run / verify
1. `dotnet build` in `apps/api/Jobuler.Api` — should succeed with no errors
2. Login with email: `POST /auth/login { "identifier": "user@example.com", "password": "..." }`
3. Login with phone: `POST /auth/login { "identifier": "0501234567", "password": "..." }`
4. Legacy login still works: `POST /auth/login { "email": "user@example.com", "password": "..." }`
5. Register with phone only: `POST /auth/register { "displayName": "Test", "password": "Pass1234", "phoneNumber": "+972501234567" }`
6. Frontend login page shows "Email or Phone Number" label with text input

## What comes next
- Phone number verification via SMS (OTP)
- Phone number normalization with libphonenumber for international format support

## Git commit
```bash
git add -A && git commit -m "feat(auth): login by email or phone, optional email registration"
```
