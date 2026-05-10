# 124 — Rate Limiting & Platform Admin DB Flag

## Phase
Phase 8 — Security Hardening

## Purpose
1. **Rate limiting**: Protect the API from abuse by limiting each IP to 100 requests per minute globally. Exceeding returns HTTP 429.
2. **Platform admin flag**: Replace the hardcoded UUID check in `PlatformController` and the frontend `AppShell` with a proper `is_platform_admin` boolean column in the `users` table. The login response now includes `isPlatformAdmin` so the frontend can show/hide the platform nav link without an extra API call.

## What was built

| File | Change |
|------|--------|
| `infra/migrations/037_platform_admin_flag.sql` | New migration: adds `is_platform_admin` column, seeds the existing platform owner |
| `apps/api/Jobuler.Domain/Identity/User.cs` | Added `IsPlatformAdmin` property |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/UserConfiguration.cs` | Maps `is_platform_admin` column |
| `apps/api/Jobuler.Api/Controllers/PlatformController.cs` | Queries DB for `IsPlatformAdmin` instead of comparing to hardcoded GUID |
| `apps/api/Jobuler.Application/Auth/Commands/LoginCommand.cs` | Added `IsPlatformAdmin` to `LoginResult` record |
| `apps/api/Jobuler.Application/Auth/Commands/LoginCommandHandler.cs` | Passes `user.IsPlatformAdmin` to `LoginResult` |
| `apps/api/Jobuler.Application/Auth/Commands/RefreshTokenCommandHandler.cs` | Passes `user.IsPlatformAdmin` to `LoginResult` on token refresh |
| `apps/api/Jobuler.Api/Program.cs` | Added global rate limiter (100 req/min per IP, queue 10) |
| `apps/web/lib/api/auth.ts` | Added `isPlatformAdmin` to `LoginResponse` interface |
| `apps/web/lib/store/authStore.ts` | Stores `isPlatformAdmin` from login response |
| `apps/web/components/shell/AppShell.tsx` | Uses `isPlatformAdmin` from store instead of hardcoded UUID comparison |

## Key decisions
- **Global + named rate limiters**: The global limiter (100/min per IP) applies to all endpoints. The existing named `auth` limiter (10/min in prod) still applies additionally to auth endpoints for brute-force protection.
- **DB flag over role table**: A simple boolean on the `users` table is sufficient for a single platform admin. No need for a full RBAC system at this stage.
- **Login response includes admin flag**: Avoids an extra `/auth/me` or `/platform/check` call on every page load.

## How it connects
- `PlatformController` now depends on `AppDbContext` directly (injected via DI) to query the user's admin flag.
- The frontend auth store persists `isPlatformAdmin` so the platform nav link renders immediately on page load.
- The rate limiter sits in the middleware pipeline before authentication, protecting all endpoints including unauthenticated ones.

## How to run / verify
1. **API build**: `dotnet build` in `apps/api` — Domain, Infrastructure, and Api projects compile (pre-existing `SmartImportCommand.cs` errors are unrelated).
2. **Frontend build**: `npm run build` in `apps/web` — passes.
3. **Migration**: Already applied via `docker exec compose-postgres-1 psql ...`.
4. **Rate limiting**: Send >100 requests in 1 minute from the same IP → expect HTTP 429.
5. **Platform admin**: Login as the seeded admin user → response includes `isPlatformAdmin: true`. Platform nav link appears. Other users get `isPlatformAdmin: false` and no nav link.

## What comes next
- Consider adding a UI for granting/revoking platform admin (currently only via direct DB update).
- Add rate limit headers (`X-RateLimit-Remaining`, `Retry-After`) to 429 responses for better client UX.

## Git commit

```bash
git add -A && git commit -m "feat(security): rate limiting (100 req/min per IP), platform admin DB flag replaces hardcoded UUID"
```
