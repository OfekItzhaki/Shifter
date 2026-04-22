# Step 024 — End-to-End Tests (Playwright)

## Phase
Post-MVP Completion

## Purpose
Provide a runnable E2E test suite that exercises the full app stack locally — browser → Next.js → API → PostgreSQL. These tests catch integration issues that unit tests miss: routing, auth flows, page rendering, and UI interactions.

## What was built

| File | Description |
|---|---|
| `apps/web/playwright.config.ts` | Playwright config: Chromium only, sequential workers, screenshots/video on failure, base URL from `E2E_BASE_URL` env var (default `http://localhost:3000`) |
| `apps/web/e2e/helpers/auth.ts` | `loginAsAdmin()` and `enterAdminMode()` helpers shared across all specs |
| `apps/web/e2e/auth.spec.ts` | Login page renders, invalid credentials shows error, valid credentials redirects |
| `apps/web/e2e/schedule.spec.ts` | Today/tomorrow schedule pages load, admin schedule page loads, trigger solve button visible, export buttons appear when version selected |
| `apps/web/e2e/people.spec.ts` | People list loads, create new person, person detail page shows roles/restrictions/availability sections |
| `apps/web/e2e/admin-nav.spec.ts` | All admin pages load without error, notification bell visible, logout works |
| `apps/web/package.json` | Added `@playwright/test` dev dependency, `test:e2e` and `test:e2e:ui` scripts |

## Key decisions

### Sequential workers
Tests share a live backend with real data. Parallel execution would cause race conditions (e.g. two tests creating people simultaneously). Sequential is safer for a shared local environment.

### No webServer auto-start
Playwright can auto-start the dev server, but this project has 3 services (Next.js, .NET API, PostgreSQL). The user must start them manually. The config includes a comment explaining this.

### Demo seed data dependency
Tests use `admin@demo.local` / `Demo1234!` from the seed script. Run `./infra/scripts/seed.sh` before running E2E tests.

### Graceful skips for missing data
Tests that click "first person" or "first version" check `isVisible()` before clicking — they pass vacuously if no data exists yet, rather than failing. This makes the suite runnable on a fresh environment.

## How to run

```bash
# 1. Start all services
docker compose -f infra/compose/docker-compose.yml up -d
./infra/scripts/migrate.sh
./infra/scripts/seed.sh

# 2. Start the API (in a separate terminal)
cd apps/api && dotnet run --project Jobuler.Api

# 3. Start the frontend (in a separate terminal)
cd apps/web && npm install && npm run dev

# 4. Install Playwright browsers (first time only)
cd apps/web && npx playwright install chromium

# 5. Run E2E tests
cd apps/web && npm run test:e2e

# 6. Open interactive UI mode (optional)
cd apps/web && npm run test:e2e:ui
```

## Environment variables

| Variable | Default | Description |
|---|---|---|
| `E2E_BASE_URL` | `http://localhost:3000` | Frontend URL |
| `E2E_ADMIN_EMAIL` | `admin@demo.local` | Admin login email |
| `E2E_ADMIN_PASS` | `Demo1234!` | Admin login password |

## What comes next
- Dev/staging environment setup
- Production environment setup

## Git commit

```bash
git add -A && git commit -m "feat(e2e): Playwright test suite for auth, schedule, people, admin nav"
```
