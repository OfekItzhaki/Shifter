# 167 — Email Verified User Flag

## Phase

Email Verification Feature — Domain Layer

## Purpose

Add an `EmailVerified` flag to the `User` entity so the system can track whether a user has verified their email address. This is the foundation for the non-blocking email verification flow.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Identity/User.cs` | Added `EmailVerified` property (default `false`) and `MarkEmailVerified()` method |

## Key decisions

- `EmailVerified` defaults to `false` so all existing users are treated as unverified until they explicitly verify.
- `MarkEmailVerified()` calls `Touch()` to update `UpdatedAt`, following the same pattern as other state-changing methods on the entity (e.g., `Deactivate()`, `UpdateProfile()`).
- The property uses a `private set` to enforce that verification can only happen through the domain method.

## How it connects

- **Requirement 3.4**: New users start with `EmailVerified = false`.
- **Requirement 4.1**: The `VerifyEmailCommand` handler will call `MarkEmailVerified()` after validating a token.
- **Requirement 9.3**: The EF Core configuration (task 2.1) will map this to an `email_verified` column with a `false` default.
- The `VerificationBanner` component will read this flag from the `/auth/me` response.

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build
```

Build should succeed with no errors or warnings.

## What comes next

- Task 2.1: EF Core configuration for the new `email_verified` column and the `EmailVerificationToken` entity.
- Task 2.2: EF Core migration to apply schema changes.

## Git commit

```bash
git add -A && git commit -m "feat(email-verification): add EmailVerified flag to User entity"
```
