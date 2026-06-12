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
  dependency-ordered writes, migrated PostgreSQL compatibility fields, and
  imported user shells that avoid exporting password hashes.
- Wires `FIELD_ENCRYPTION_KEY` through customer compose configuration and makes
  it required by the customer-hosted env validator.
- Adds a customer-hosted entitlement startup guard and env validation for
  `SHIFTER_LICENSEE` and `SHIFTER_LICENSE_KEY`, so private installs cannot
  start from an unlicensed placeholder env file.
- Supports signed offline license files for private-network installs via
  `SHIFTER_LICENSE_FILE_CONTAINER_PATH` and `SHIFTER_LICENSE_PUBLIC_KEY`, with
  RSA signature verification at API startup.
- Adds `infra/scripts/generate-signed-license.ps1` so OfekLabs can issue signed
  customer license JSON files from a protected RSA private key.
- Adds a Windows/PowerShell customer env validator alongside the Bash validator.
- Lets `infra/scripts/test-customer-hosted-package.ps1` run against a real
  customer env file with `-EnvFile ... -ValidateEnvFile`, while retaining the
  template/package preflight path.
- Adds `infra/scripts/verify-customer-hosted-install.ps1` as the target-host
  wrapper for real env validation, package preflight, seeded demo data setup,
  and live self-service smoke checks.
- Adds a top-level `/ready` API readiness probe for orchestrators and deploy
  scripts, and switches Compose/deploy/ECS health checks to readiness instead
  of the broader `/health` endpoint.
- Adds `infra/scripts/smoke-self-service-client-ready.ps1` to preflight web/API
  health, seeded demo users, the self-service demo cycle, member/admin
  self-service workflow read models, available slots, admin assignment reads,
  cycle closeout metrics, and the customer-hosted restore script syntax, plus
  the special-day Playwright picker flow.
- Adds `infra/scripts/seed-compose.sh` so customer-hosted demo smoke data can be
  loaded through Docker Compose without requiring `psql` on the host, including
  a dry-run mode for validating the resolved target.
- Adds `infra/scripts/bundle-compose-images.sh` for restricted-network
  customer installs, building Shifter images, pulling bundled infrastructure
  images, and saving a Docker image tarball with manifest and checksum.
- Adds `infra/scripts/package-customer-hosted.ps1` for customer handoff
  archives, including a manifest, `.sha256` checksum sidecar, dotfile-safe zip
  creation, private material exclusion checks, extracted-package Compose
  validation, and dry-run verification from the extracted archive.
- Adds `docs/CUSTOMER-HOSTED-HANDOFF-NOTES.md` as the go-live handoff template
  for package checksum, license, domains, env/secrets ownership, provider
  approvals, verification evidence, backup/restore, migration, security, and
  escalation sign-off.
- Records closeout CSV/PDF export smoke evidence, closeout PDF fingerprint
  checks, and the customer-specific closeout report signing policy in the
  customer-hosted handoff template.
- Lets the live self-service smoke read `APP_FRONTEND_BASE_URL`,
  `NEXT_PUBLIC_API_URL`/`APP_API_BASE_URL`, and optional seeded demo credentials
  from the customer env file with `-EnvFile`, plus `-ResolveOnly` for dry config
  checks.
- Supports `-SkipBrowserTest` for API/seed preflight when the web app is not
  running.
- Adds AI support fallback coverage for private/local AI endpoint failure and
  readable localized support actions when AI is disabled.
- Enforces no-export AI mode at API startup and in customer Compose config, so
  `AI_NO_EXPORT_REQUIRED=true` cannot silently use a public hosted endpoint.
- Validates private AI endpoint configuration at API startup: `AI_BASE_URL`
  must be absolute and `AI_MODEL` must be explicit when a customer-hosted
  endpoint is configured.
- Routes native assistant, landing, profile feedback, and accessibility contact
  entry points to the configured `NEXT_PUBLIC_LEGAL_EMAIL`, so customer-hosted
  installs can point support requests at the customer's own help address.
- Adds a Cloudflare edge security baseline covering DNS/TLS, WAF/rate-limit
  rules for auth, billing, imports, solver triggers, uploads, and admin paths,
  plus PWA/API caching guidance.
- Adds a hosted VPS MVP launch checklist that separates OfekLabs-managed
  hosted-service rollout from customer-hosted package delivery, covering the
  smoke checks, backups, providers, PWA, AI, monitoring, and edge-security
  items to verify before inviting real users.
- Requires `NEXT_PUBLIC_LEGAL_EMAIL` in customer env validation so private
  installs do not silently route support/contact UI to the SaaS fallback.
- Wires public frontend deployment variables through the web Docker build and
  runtime environment, including legal/support email, Crisp, PostHog, Sentry,
  VAPID, app version, and the public API URL.
- Keeps Sentry disabled unless `NEXT_PUBLIC_SENTRY_DSN` is explicitly
  configured, matching customer-hosted installs where error tracking is not
  approved.
- Keeps PostHog disabled unless `NEXT_PUBLIC_POSTHOG_KEY` is explicitly
  configured in production, including direct identify/track/reset calls.
- Keeps Crisp disabled unless `NEXT_PUBLIC_CRISP_WEBSITE_ID` is explicitly
  configured, trimming accidental whitespace before loading the widget.
- Adds `infra/scripts/restore-compose.sh` with `DRY_RUN=1` preflight support,
  automatic database/uploads pre-restore safety dumps, transactional
  `pg_restore`, app-service restart on restore-script failure, and restore
  runbook docs for
  customer-hosted PostgreSQL dumps and optional uploads-volume archives.
- Adds organization-level self-service default templates for multi-space
  customers. Spaces now resolve first-time self-service group policy from space
  defaults, then organization defaults, then install env defaults, and
  organization templates are included in export/import package validation.
- Adds a Platform organization self-service defaults panel so platform admins
  can edit multi-space customer policy templates without calling the API
  directly.
- Clarifies the holiday/manual self-service boundary: special-day labels,
  cycle counts, and underfilled-slot warnings are covered now; holiday-specific
  staffing policy changes remain future scope.
- Adds the first special-day self-service policy rule: slots on special days
  with `requiresCoverage=false` remain visible with no-coverage labels, but
  member picks, waitlist joins, waitlist offer cascades, and stale offer
  acceptance are blocked. Admin overrides remain the explicit exception path.
- Extends the special-day policy to special leave requests: requests overlapping
  no-coverage special days are rejected, while coverage-required special-day
  overlaps are allowed and highlighted to admins.
- Adds special-day impact to cycle closeout and exports: total special-day
  slots, no-coverage special-day slots, and underfilled special-day slots.
- Adds active member workflow policy flags to closeout responses, CSV exports,
  and PDF reports, plus a compact Operations UI policy snapshot.
- Exposes member workflow policy flags in the My Shifts response and hides
  member change, absence, and swap actions when the group policy disables them.
- Exposes pick/waitlist policy flags in the Available Slots response and shows
  disabled policy labels instead of pick or waitlist buttons.
- Ends the current session after password changes and shows a login success
  notice, preventing stale authenticated sessions after credential rotation.
- Supports PWA install prompts on mobile and desktop when the browser reports
  install availability, uses device-neutral copy, and caches/refreshes
  self-service member read models for installed/offline usage.
- Adds installed-PWA shortcuts for personal shifts, self-service shift picking,
  and profile/account tools.
- Refreshes member-safe self-service cache entries on reconnect for the current
  group and last picked self-service group, while avoiding admin-only closeout
  refreshes for normal members.

## Verification

- `dotnet build apps\\api\\Jobuler.sln` passed.
- `dotnet test apps\\api\\Jobuler.sln` passed: 1,949 passed, 12 skipped,
  0 failed.
- `node_modules\\.bin\\next.cmd build` from `apps/web` passed.
- `node_modules\\.bin\\eslint.cmd .` from `apps/web` passed with existing
  warnings and 0 errors.
- `node_modules\\.bin\\vitest.cmd run __tests__\\selfService\\slotBrowserTab.test.tsx __tests__\\selfService\\cycleControlPanel.test.tsx`
  passed: 7 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~SlotAvailabilityEngineTests|FullyQualifiedName~ManualSelfServiceLifecycleTests|FullyQualifiedName~WaitlistServiceTests"`
  passed: 42 passed, 0 failed, including no-coverage special-day member action
  policy coverage.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~SpecialLeaveRequestCommandTests`
  passed: 18 passed, 0 failed, including special leave overlap handling for
  no-coverage and coverage-required special days.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~SelfServiceScopeTests.GetCycleCloseout|FullyQualifiedName~SelfServiceScopeTests.ExportCloseout"`
  passed: 3 passed, 0 failed, including closeout, CSV, and PDF special-day
  impact metrics.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~SelfServiceScopeTests.ExportCloseout`
  passed: 2 passed, 0 failed, including proof that the closeout PDF
  verification fingerprint matches the SHA-256 of the exported closeout CSV
  bytes.
- `node_modules\\.bin\\vitest.cmd run __tests__\\selfService\\slotBrowserTab.test.tsx`
  passed: 6 passed, 0 failed, including no-coverage special-day and disabled
  pick/waitlist policy states.
- `node_modules\\.bin\\vitest.cmd run __tests__\\selfService\\selfServiceOperationsTab.test.tsx`
  passed: 1 passed, 0 failed, including the closeout special-day impact row.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~SelfServiceScopeTests.ListMine_CurrentShiftCount_CountsOnlyApprovedAssignments`
  passed: 1 passed, 0 failed, including My Shifts workflow policy flags.
- `node_modules\\.bin\\vitest.cmd run __tests__\\selfService\\myShiftsTab.test.tsx`
  passed: 11 passed, 0 failed, including hidden member actions when workflows
  are disabled.
- `node_modules\\.bin\\eslint.cmd app\\groups\\[groupId]\\tabs\\SlotBrowserTab.tsx lib\\api\\selfService.ts __tests__\\selfService\\slotBrowserTab.test.tsx`
  passed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~SelfServiceScopeTests.GetAvailable_AllowsGroupMemberWithoutSpaceViewGrant`
  passed: 1 passed, 0 failed, including Available Slots pick/waitlist policy
  flags.
- `node_modules\\.bin\\eslint.cmd components\\groups\\selfService\\SelfServiceOperationsTab.tsx lib\\api\\selfService.ts __tests__\\selfService\\selfServiceOperationsTab.test.tsx`
  passed.
- `node_modules\\.bin\\eslint.cmd components\\groups\\selfService\\MyShiftsTab.tsx lib\\api\\selfService.ts __tests__\\selfService\\myShiftsTab.test.tsx`
  passed.
- `node_modules\\.bin\\eslint.cmd e2e\\self-service.browser.spec.ts` passed.
- `node_modules\\.bin\\playwright.cmd test self-service.browser.spec.ts --list`
  discovered 15 browser lifecycle tests, including the special-day label flow
  and final-state slot fill-count coverage for picks, waitlist join/leave,
  shift cancellation, shift-change approval/rejection, swap
  accept/decline/cancel, and cannot-attend rejection.
- `infra/scripts/smoke-self-service-client-ready.ps1 -ApiBaseUrl http://localhost:5015 -WebBaseUrl http://localhost:3015`
  passed against a fresh SQL install from all migrations plus `seed.sql`, a
  live API, and a rebuilt production web server. This covered customer-hosted
  restore script syntax, seeded demo users, the self-service demo cycle,
  member/admin workflow read models, available-slot/member/closeout workflow
  policy flags, admin assignment reads, seeded special-day closeout impact counts,
  closeout CSV archive policy/special-day rows, closeout PDF download checks,
  web reachability, and the
  Playwright special-day picker browser flow.
- `infra/scripts/smoke-self-service-client-ready.ps1 -EnvFile infra/compose/.env.customer.example -ResolveOnly`
  passed, proving the live smoke can derive customer target URLs from the same
  env file used by Compose.
- `infra/scripts/validate-customer-env.ps1` parser check passed locally.
- `infra/scripts/test-customer-env-validator.ps1` passed, covering valid env,
  private no-export AI, public no-export AI rejection, and short
  `FIELD_ENCRYPTION_KEY` rejection, missing/short `SHIFTER_LICENSE_KEY`
  rejection, signed offline license-file acceptance, and partial signed-license
  rejection across the PowerShell validator and Git Bash validator when
  available. It also asserts customer-hosted warning output when optional
  external processors such as PostHog, Sentry, Crisp, or LemonSqueezy are
  configured.
- Customer env validators now fail partial optional provider groups for Resend,
  Twilio, Web Push VAPID, Pushover, and LemonSqueezy before deployment.
- Provider health checks treat unconfigured LemonSqueezy billing as skipped,
  report partial billing configuration with exact missing keys, and expose
  core/optional provider readiness in the platform UI.
- `infra/scripts/test-restore-compose-dry-run.ps1` passed, proving
  `restore-compose.sh` honors a custom `ENV_FILE` during dry-run Compose
  validation.
- `infra/scripts/test-backup-compose.ps1` passed with a fake Docker shim,
  proving `backup-compose.sh` passes custom `ENV_FILE` and project name to
  Compose while producing a non-empty database dump.
- `infra/scripts/test-deploy-compose.ps1` passed with fake Git, Docker, and
  Curl shims, proving `deploy-compose.sh` fetches/pulls the target ref, runs a
  pre-deploy database backup, starts Compose with the configured env/project,
  and checks web/API health without touching real services.
- `infra/scripts/test-deploy-compose-rollback.ps1` passed with fake Git,
  Docker, and Curl shims, proving a failed deploy health check rolls back to
  the previous git revision, restarts Compose, and verifies rollback health.
- `infra/scripts/test-customer-hosted-package.ps1` passed, running the customer
  env validator, restore dry-run, seed dry-run, backup, deploy happy-path,
  deploy rollback, Compose script syntax, customer Docker Compose config
  checks, and the PostgreSQL organization import smoke as one preflight command.
- `infra/scripts/test-package-customer-hosted.ps1` passed, proving the
  customer handoff package creates a zip plus `.sha256` checksum, extracts
  cleanly with dotfiles such as `.env.customer.example`, validates the
  extracted Compose config, asserts non-default self-service install defaults
  render into the API environment as exact `SelfServiceDefaults__*` values,
  blocks obvious private material, and dry-runs the packaged install verifier
  from the extracted directory.
- `infra/scripts/test-seed-compose-dry-run.ps1` passed, proving the compose seed
  loader resolves the env file, Compose project, database, user, and seed file
  without loading demo data.
- `infra/scripts/test-generate-signed-license.ps1` passed, proving the license
  generator writes a signed JSON license and verifiable RSA public key.
- `infra/scripts/test-bundle-compose-images.ps1` passed with a fake Docker
  shim, proving the image bundle script builds app services, pulls bundled
  infrastructure services, writes a manifest, and saves the resolved image set.
- `infra/scripts/verify-customer-hosted-install.ps1 -EnvFile infra/compose/.env.customer.example -SkipPackagePreflight -SeedDryRun -ResolveOnly`
  passed, proving the target-host wrapper can dry-run seeded data and live smoke
  configuration from one command.
- `infra/scripts/test-verify-customer-hosted-install-dry-run.ps1` passed, and is
  included in the customer-hosted package preflight.
- `infra/scripts/test-customer-hosted-package.ps1 -EnvFile infra/compose/.env.customer.example`
  passed, proving the preflight can target an explicit env file path without
  requiring real customer secrets in CI/local package checks.
- `infra/scripts/test-customer-hosted-package.ps1 -EnvFile infra/compose/.env.customer.example`
  passed again after adding the handoff notes template to the package, proving
  the customer archive still assembles, extracts, validates Compose, verifies
  checksum output, and runs the PostgreSQL organization import smoke.
- `.github/workflows/customer-hosted-preflight.yml` now runs
  `infra/scripts/test-customer-hosted-package.ps1 -EnvFile infra/compose/.env.customer.example`
  on relevant PRs, pushes to this branch, and manual dispatch, so the
  customer-hosted package checks cannot quietly drift.
- The customer-hosted preflight watches the same package-relevant surface the
  archive includes: API, web, solver, Docker/Compose/migrations/scripts, and
  customer-facing handoff docs.
- Customer-hosted preflight CI passed on this branch; see the workflow history:
  https://github.com/OfekItzhaki/Shifter/actions/workflows/customer-hosted-preflight.yml
- Customer-hosted preflight CI run `27425585221` passed for commit `f6dfe0a2`
  on June 12, 2026: `Package Preflight` completed successfully in 1m 58s.
- GitHub Actions first-party setup actions were upgraded to Node 24-compatible
  majors after that run to avoid the upcoming Node 20 action-runtime
  deprecation warning.
- Customer-hosted preflight CI run `27431644839` passed for commit `031e0bb`
  on June 12, 2026: `Package Preflight` completed successfully after the
  action-major upgrades.
- Before merge, use the workflow history above to confirm the latest
  package-relevant run for `feat/self-service-client-ready` is green.
- `infra/scripts/backup-compose.sh`, `infra/scripts/deploy-compose.sh`,
  `infra/scripts/restore-compose.sh`, and `infra/scripts/seed-compose.sh`
  syntax checks passed after wiring custom `ENV_FILE` through their Compose
  calls.
- `C:\\Program Files\\Git\\bin\\bash.exe -n infra/scripts/restore-compose.sh`
  passed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~AiAssistantSupportTests`
  passed: 6 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~AiConfigurationGuardTests|FullyQualifiedName~AiAssistantSupportTests|FullyQualifiedName~AiHealthCheckTests"`
  passed: 30 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~HealthEndpointIntegrationTests|FullyQualifiedName~AiHealthCheckTests"`
  passed: 21 passed, 0 failed, including detailed health provider metadata
  preservation for customer-hosted AI deployment mode reporting.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~SelfServiceDefaultPolicyOptionsTests|FullyQualifiedName~ChangeSchedulingModeCommandTests"`
  passed: 29 passed, 0 failed, including startup-validation coverage for
  install-level self-service defaults.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~DeploymentEntitlementGuardTests`
  passed: 11 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~ResendHealthCheckTests|FullyQualifiedName~ResendEmailSenderTests"`
  passed: 5 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~HealthChecks`
  passed: 49 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~HealthEndpointIntegrationTests`
  passed after adding `/ready`.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~SpaceSpecialDayCommandTests`
  passed: 4 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~OrganizationPortabilityTests`
  passed: 23 passed, 0 failed, including export/import executor coverage for
  the full manual self-service graph.
- `infra\\scripts\\smoke-organization-import-postgres.ps1` passed, applying all
  SQL migrations to a temporary PostgreSQL 16 container and importing a
  validated organization package into that migrated target.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~PlatformControllerImportTests`
  passed: 2 passed, 0 failed.
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
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~SelfServiceScopeTests|FullyQualifiedName~ManualSelfServiceLifecycleTests"`
  passed: 61 passed, 0 failed, including disabled workflow policy coverage for
  shift-change request submission.
- Fresh SQL install from all `infra/migrations/*.sql` plus `seed.sql` passed
  after adding `086_organization_self_service_defaults.sql`.
- `node_modules\\.bin\\vitest.cmd run __tests__\\shell\\pwaInstallPrompt.test.tsx`
  passed: 2 passed, 0 failed.
- `node_modules\\.bin\\eslint.cmd components\\shell\\PwaInstallPrompt.tsx __tests__\\shell\\pwaInstallPrompt.test.tsx`
  passed.
- `node_modules\\.bin\\vitest.cmd run __tests__\\cache\\backgroundRefresh.test.ts __tests__\\cache\\cacheLifecycle.test.ts`
  passed: 6 passed, 0 failed.
- `node_modules\\.bin\\eslint.cmd lib\\cache\\backgroundRefresh.ts lib\\hooks\\useCacheLifecycle.ts __tests__\\cache\\backgroundRefresh.test.ts __tests__\\cache\\cacheLifecycle.test.ts`
  passed.
- `node --check public\\sw.js` from `apps/web` passed.
- `node_modules\\.bin\\vitest.cmd run __tests__\\pwa\\manifest.test.ts`
  passed, confirming install metadata and `/schedule/my-missions`, `/pick`,
  and `/profile` shortcuts reference existing icon assets.
- `node_modules\\.bin\\vitest.cmd run __tests__\\landing\\landingContent.test.ts`
  passed, confirming landing PWA copy advertises phone and desktop install
  support without regressing to phone-only wording.
- `node_modules\\.bin\\next.cmd build` from `apps/web` passed after the PWA
  prompt update.
- `node_modules\\.bin\\vitest.cmd run __tests__\\monitoring\\sentryConfig.test.ts __tests__\\monitoring\\posthogConfig.test.ts __tests__\\monitoring\\crispConfig.test.ts`
  passed: 6 passed, 0 failed.
- `node_modules\\.bin\\eslint.cmd sentry.client.config.ts sentry.server.config.ts lib\\monitoring\\sentryConfig.ts __tests__\\monitoring\\sentryConfig.test.ts`
  passed.
- `node_modules\\.bin\\eslint.cmd lib\\analytics\\posthog.ts lib\\analytics\\posthogConfig.ts __tests__\\monitoring\\posthogConfig.test.ts`
  passed.
- `node_modules\\.bin\\tsc.cmd --noEmit --pretty false` from `apps/web`
  passed after tightening PostHog key narrowing.
- `node_modules\\.bin\\vitest.cmd run __tests__\\platform\\organizationSelfServiceDefaultsPanel.test.tsx`
  passed: 2 passed, 0 failed, covering organization defaults load, edit/save,
  and validation.
- `node_modules\\.bin\\eslint.cmd components\\platform\\OrganizationSelfServiceDefaultsPanel.tsx app\\platform\\page.tsx lib\\api\\platform.ts lib\\analytics\\posthog.ts`
  passed after adding the Platform organization self-service defaults panel.
- `node_modules\\.bin\\eslint.cmd app\\layout.tsx lib\\support\\crispConfig.ts __tests__\\monitoring\\crispConfig.test.ts`
  passed.
- `node_modules\\.bin\\vitest.cmd run __tests__\\support\\contact.test.ts`
  passed, covering configured/fallback support email links and guarding
  product support entry points against hardcoded hosted support mailboxes.

## Remaining Product Checks

- On the target host, run the customer-hosted install verifier against the real
  customer env file after secrets/domains are set and the stack is running:
  `infra/scripts/verify-customer-hosted-install.ps1 -EnvFile infra/compose/.env`.
- Before a production tenant-by-tenant migration, rerun the organization import
  smoke against that customer's target PostgreSQL. Full customer-hosted
  deployment moves should still use the compose backup/restore path.

PR URL:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-client-ready
