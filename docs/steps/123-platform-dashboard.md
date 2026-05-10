# 123 — Platform Super-Admin Dashboard

## Phase

Phase 8 — Platform Operations

## Purpose

Provides the platform owner (the person who owns the Shifter deployment) with a global overview of system health and usage across all spaces. This is not a per-space admin page — it's a cross-tenant, platform-level dashboard showing aggregate metrics.

## What was built

### Backend

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Platform/Queries/GetPlatformStatsQuery.cs` | MediatR query + handler that aggregates global stats: users, spaces, groups, people, solver runs (24h), and storage counts |
| `apps/api/Jobuler.Api/Controllers/PlatformController.cs` | `GET /platform/stats` endpoint. Requires `[Authorize]` and checks the caller is the hardcoded platform owner (`a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5`). Returns 403 for anyone else. |

### Frontend

| File | Description |
|------|-------------|
| `apps/web/lib/api/platform.ts` | API client for `GET /platform/stats` with TypeScript types |
| `apps/web/app/platform/page.tsx` | Dashboard page with 3 rows of stat cards. Redirects to `/login` if unauthenticated, shows access-denied message on 403. |
| `apps/web/components/shell/AppShell.tsx` | Added "פלטפורמה" nav item, visible only when `userId === PLATFORM_OWNER_USER_ID` |

### Translations

| File | Changes |
|------|---------|
| `apps/web/messages/he.json` | Added `nav.platform` and full `platform.*` section (Hebrew) |
| `apps/web/messages/en.json` | Added `nav.platform` and full `platform.*` section (English) |
| `apps/web/messages/ru.json` | Added `nav.platform` and full `platform.*` section (Russian) |

## Key decisions

1. **Hardcoded platform owner ID** — For MVP, the platform owner is identified by a fixed UUID constant. This avoids adding a new DB column or role system for a single-user concern. Can be replaced with a config value or DB lookup later.
2. **No tenant middleware bypass** — The `PlatformController` doesn't use `TenantContextMiddleware` because it queries across all spaces. The endpoint is at `/platform/stats` (no `{spaceId}` in the route), so the tenant middleware naturally skips it.
3. **Active users via RefreshToken** — Instead of adding a `last_active_at` column, we count distinct users who received a refresh token in the last 7 days. This is a reasonable proxy for activity.
4. **Pre-existing build errors** — `SmartImportCommand.cs` has 4 pre-existing compile errors unrelated to this change. The Platform files compile cleanly.

## How it connects

- Uses the existing `AppDbContext` and all domain entities (Users, Spaces, Groups, People, ScheduleRuns, Assignments, ConstraintRules, TaskTypes, RefreshTokens)
- Follows the same MediatR query pattern as `GetBurdenStatsQuery`
- Integrates into the existing `AppShell` navigation
- Uses the existing `apiClient` with JWT auth for the frontend API call

## How to run / verify

1. **API**: `dotnet build` in `apps/api` — the Platform files compile (pre-existing errors in SmartImportCommand are unrelated)
2. **Frontend**: `npm run build` in `apps/web` — passes, `/platform` route is generated
3. **Manual test**: Log in as the platform owner user, navigate to `/platform`, verify stats load
4. **Access control**: Log in as any other user, navigate to `/platform`, verify "access denied" message appears

## What comes next

- Replace hardcoded platform owner ID with a configuration value or DB-based role
- Add time-series charts for solver performance trends
- Add space-level drill-down from the platform dashboard
- Consider caching platform stats (they're expensive cross-tenant queries)

## Git commit

```bash
git add -A && git commit -m "feat(platform): super-admin dashboard with global metrics, solver health, system stats"
```
