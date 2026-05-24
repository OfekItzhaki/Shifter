# 503 — ReAuthAttempt Domain Entity

## Phase

Admin Re-Auth Security — Backend Infrastructure

## Purpose

Introduces the `ReAuthAttempt` domain entity to track re-authentication attempts (both password and WebAuthn). This entity supports the lockout mechanism (5 failures in 15 minutes) and provides the data model for audit logging of re-auth events.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Auth/ReAuthAttempt.cs` | New domain entity with `Id`, `UserId`, `AttemptedAt`, `Success`, and `Method` properties. Includes a `Create` factory method with input validation. |

## Key decisions

- **Placed in `Jobuler.Domain/Auth/`** — A new `Auth` folder was created to separate re-auth concerns from the existing `Identity` folder (which holds user accounts and credentials). This keeps the domain organized by bounded context.
- **Extends `Entity` base class** — Inherits `Id` and `CreatedAt` from the common base. `AttemptedAt` is kept as a separate property (as specified in the design doc) for explicit semantics in lockout queries.
- **Private setters + private constructor** — Follows the project's encapsulation pattern. State can only be set through the `Create` factory method.
- **Input validation in factory** — Rejects empty `userId` and blank `method` values at the domain level, preventing invalid data from entering the system.
- **No external dependencies** — The Domain layer remains dependency-free per architecture rules.

## How it connects

- **Next step (1.2):** EF Core configuration will map this entity to the `reauth_attempts` table with a composite index on `(user_id, attempted_at DESC)`.
- **Used by (1.3):** `ReAuthenticateCommandHandler` will call `ReAuthAttempt.Create(...)` after each re-auth attempt and query recent failures for lockout logic.
- **Audit trail:** Each `ReAuthAttempt` record feeds into the security audit requirements (6.1, 6.2, 6.5).

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain/Jobuler.Domain.csproj
```

Build should succeed with zero warnings related to this file.

## What comes next

- Task 1.2: EF Core configuration and database migration for the `reauth_attempts` table.
- Task 1.3: Integration into `ReAuthenticateCommandHandler` for lockout enforcement.

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): add ReAuthAttempt domain entity for lockout tracking"
```
