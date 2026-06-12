# PR: Self-Service Client Ready

## Title

Self-service scheduling with holidays and client-hosted readiness

## Description

This PR exports the complete manual self-service scheduling stack to a single
review branch. It includes the manual member/admin workflows, holiday/special-day
integration, portable organization isolation, customer-hosted deployment
packaging, and self-service export package validation readiness.

## Highlights

- Hardens manual self-service scheduling for member picks, waitlists,
  cancellations, cannot-attend reports, shift changes, swaps, special leave,
  admin overrides, attendance, and cycle closeout.
- Adds holiday/special-day scheduling support, including Israel holiday calendar
  preview/import, and surfaces special-day labels in self-service slot browsing
  and admin cycle context.
- Integrates portable organization isolation and contact-field protection for
  future dedicated/customer-hosted installs.
- Includes self-service workflow data in organization export manifests/packages
  and import validation counts, including scoped notifications and audit logs
  tied to those workflows.
- Validates package references for exported users, owner/member links, core
  scheduling rows, and self-service workflow relationships before importing.
- Adds a conservative organization package import executor for already-safe
  packages, with explicit confirmation, target conflict checks, transactional
  writes, and imported user shells that avoid exporting password hashes.
- Wires `FIELD_ENCRYPTION_KEY` through customer compose configuration and makes
  it required by the customer-hosted env validator.
- Adds a Windows/PowerShell customer env validator alongside the Bash validator.
- Adds `infra/scripts/smoke-self-service-client-ready.ps1` to preflight web/API
  health, seeded demo users, the self-service demo cycle, available slots, and
  the customer-hosted restore script syntax, plus the special-day Playwright
  picker flow.
- Supports `-SkipBrowserTest` for API/seed preflight when the web app is not
  running.
- Adds AI support fallback coverage for private/local AI endpoint failure and
  readable localized support actions when AI is disabled.
- Routes native assistant contact payloads to the configured
  `NEXT_PUBLIC_LEGAL_EMAIL`, so customer-hosted installs can point support
  requests at the customer's own help address.
- Requires `NEXT_PUBLIC_LEGAL_EMAIL` in customer env validation so private
  installs do not silently route support/contact UI to the SaaS fallback.
- Wires public frontend deployment variables through the web Docker build and
  runtime environment, including legal/support email, Crisp, PostHog, Sentry,
  VAPID, app version, and the public API URL.
- Adds `infra/scripts/restore-compose.sh` with `DRY_RUN=1` preflight support,
  automatic database/uploads pre-restore safety dumps, transactional
  `pg_restore`, app-service restart on restore-script failure, and restore
  runbook docs for
  customer-hosted PostgreSQL dumps and optional uploads-volume archives.
- Adds organization-level self-service default templates for multi-space
  customers. Spaces now resolve first-time self-service group policy from space
  defaults, then organization defaults, then install env defaults, and
  organization templates are included in export/import package validation.
- Ends the current session after password changes and shows a login success
  notice, preventing stale authenticated sessions after credential rotation.
- Keeps the custom PWA install prompt focused on mobile/touch install surfaces
  while desktop users can still use the browser's native install affordance.

## Verification

- `dotnet build apps\\api\\Jobuler.sln` passed.
- `dotnet test apps\\api\\Jobuler.sln` passed: 1,949 passed, 12 skipped,
  0 failed.
- `node_modules\\.bin\\next.cmd build` from `apps/web` passed.
- `node_modules\\.bin\\eslint.cmd .` from `apps/web` passed with existing
  warnings and 0 errors.
- `node_modules\\.bin\\vitest.cmd run __tests__\\selfService\\slotBrowserTab.test.tsx __tests__\\selfService\\cycleControlPanel.test.tsx`
  passed: 7 passed, 0 failed.
- `node_modules\\.bin\\eslint.cmd e2e\\self-service.browser.spec.ts` passed.
- `node_modules\\.bin\\playwright.cmd test self-service.browser.spec.ts --list`
  discovered 14 browser lifecycle tests, including the special-day label flow.
- `infra/scripts/smoke-self-service-client-ready.ps1 -ApiBaseUrl http://localhost:5015 -WebBaseUrl http://localhost:3015`
  passed against a fresh SQL install from all migrations plus `seed.sql`, a
  live API, and a rebuilt production web server. This covered customer-hosted
  restore script syntax, seeded demo users, the self-service demo cycle,
  available slots, web reachability, and the Playwright special-day picker
  browser flow.
- `infra/scripts/validate-customer-env.ps1` parser check passed locally.
- `C:\\Program Files\\Git\\bin\\bash.exe -n infra/scripts/restore-compose.sh`
  passed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~AiAssistantSupportTests`
  passed: 6 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~SpaceSpecialDayCommandTests`
  passed: 4 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~OrganizationPortabilityTests`
  passed: 23 passed, 0 failed.
- `node_modules\\.bin\\eslint.cmd components\\spaces\\SpecialDaysCard.tsx lib\\api\\spaceSpecialDays.ts`
  passed.
- `node_modules\\.bin\\eslint.cmd components\\shell\\ShifterAssistant.tsx`
  passed.
- `docker compose -f infra\\compose\\docker-compose.yml config --quiet`
  passed after public frontend env wiring.
- `docker compose --env-file infra\\compose\\.env.customer.example -f infra\\compose\\docker-compose.yml config --quiet`
  passed, proving the checked-in customer env template renders through Compose.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~ChangeSchedulingModeCommandTests|FullyQualifiedName~OrganizationPortabilityTests"`
  passed: 37 passed, 0 failed.
- Fresh SQL install from all `infra/migrations/*.sql` plus `seed.sql` passed
  after adding `086_organization_self_service_defaults.sql`.
- `node_modules\\.bin\\vitest.cmd run __tests__\\shell\\pwaInstallPrompt.test.tsx`
  passed: 2 passed, 0 failed.
- `node_modules\\.bin\\eslint.cmd components\\shell\\PwaInstallPrompt.tsx __tests__\\shell\\pwaInstallPrompt.test.tsx`
  passed.
- `node_modules\\.bin\\next.cmd build` from `apps/web` passed after the PWA
  prompt update.

## Remaining Product Checks

- Run a customer-hosted smoke with real customer secrets and a real database.
- Smoke-test organization package import against a real PostgreSQL target before
  promising tenant-by-tenant migration in production. Full customer-hosted
  deployment moves should still use the compose backup/restore path.

PR URL:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-client-ready
