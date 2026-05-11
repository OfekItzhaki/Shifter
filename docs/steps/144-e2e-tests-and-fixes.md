# 144 — E2E Tests & Fixes

## Phase
Phase 8 — Quality

## Purpose
Add Playwright E2E tests for the new features (landing page, join page, settings) and fix the docker-compose version warning.

## What was built

### Files created:

| File | Description |
|------|-------------|
| `apps/web/e2e/landing.spec.ts` | Tests for landing page, terms, privacy, join page |
| `apps/web/e2e/settings.spec.ts` | Tests for profile page, notification preferences, dark mode toggle |

### Files modified:

| File | Change |
|------|--------|
| `infra/compose/docker-compose.yml` | Removed obsolete `version: "3.9"` line |
| `apps/web/app/LandingPage.tsx` | Added `dir="ltr"`, removed free badge |

## Test coverage

### Landing page tests:
- Landing page renders for unauthenticated users
- Sign in and register buttons visible
- Features section visible
- FAQ items expandable
- Navigation links scroll to sections
- Terms page renders
- Privacy page renders

### Join page tests:
- Join form renders
- Login prompt for unauthenticated users

### Settings tests:
- Profile page shows notification preferences
- Dark mode toggle exists
- Notification toggles work (click changes state)

## How to run
```bash
cd apps/web && npx playwright test e2e/landing.spec.ts e2e/settings.spec.ts
```

## Git commit

```bash
git add -A && git commit -m "test: e2e tests for landing, join, settings + docker-compose fix"
```
