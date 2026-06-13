# Self-Service Branch Stack Status

Use this as the current branch-stack map for manual self-service,
holiday-calendar integration, and customer-hosted portability.

## Current Resume Point

- Active integration branch: `develop`
- `feat/self-service-client-ready` was squash-merged into `develop` by PR
  `#36` on June 12, 2026 as commit `20fb7d3`.
- Current pushed `develop` commit:

  ```powershell
  git log -1 --oneline origin/develop
  ```

- Current release gate status:

  - Check the latest `develop` runs in GitHub Actions for `CI`,
    `Customer-Hosted Preflight`, and `Deploy Staging`.
  - `Deploy Staging` is expected to skip while
    `ENABLE_STAGING_DEPLOY=false`.
  - `infra/scripts/check-release-readiness.ps1 -SkipHostedSmoke` fails as
    intended until `STAGING_WEB_BASE_URL`, `STAGING_API_BASE_URL`, and a
    successful staging deploy for the current `develop` head exist. Use
    `-RequireDedicatedStagingSecrets` for the final `develop` to `main` gate.

- Package-relevant CI history:
  https://github.com/OfekItzhaki/Shifter/actions/workflows/customer-hosted-preflight.yml
- Resume on another machine with:

  ```powershell
  git fetch origin
  git checkout develop
  git pull --ff-only
  ```

## Integration Status

The historical branch stack was:

1. `feat/manual-self-service-hardening`
2. `feat/self-service-holiday-integration`
3. `feat/self-service-portable-integration`
4. `feat/self-service-portability-export-readiness`
5. `feat/self-service-client-ready`

That stack is now represented in `develop` through the PR `#36` squash merge.
Do not mechanically merge those old branch tips into `develop`: because PR `#36`
was squash-merged and later release-gate commits landed on `develop`, raw
ahead/behind counts make the old branches look unmerged and their diffs include
removing newer staging/release-readiness work. If a later audit finds a specific
missing change, cherry-pick that exact commit or reapply the exact patch after
review, then verify it from `develop`.

The next release branch should be `develop` to `main` only after staging URLs,
staging deployment, hosted smoke, and manual user-flow evidence are complete.

## Branches

### `feat/manual-self-service-hardening`

Purpose: harden manual self-service scheduling for a customer-hosted MVP.

PR:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/manual-self-service-hardening

PR summary:

[Manual self-service PR summary](PULL_REQUEST_MANUAL_SELF_SERVICE_HARDENING.md)

Known verification:

- Frontend build passed.
- Frontend lint passed with existing warnings and 0 errors.
- Self-service browser test discovery found 13 lifecycle tests.
- Targeted backend self-service tests passed.
- Full API suite passed: 1,921 passed, 12 skipped, 0 failed.

### `feat/self-service-holiday-integration`

Purpose: integrate holiday/special-day support on top of manual self-service.

PR:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-holiday-integration

PR summary:

[Holiday integration PR summary](PULL_REQUEST_SELF_SERVICE_HOLIDAY_INTEGRATION.md)

Known verification:

- API build passed.
- Targeted API tests passed: 206 passed, 0 failed.
- Holiday-aware self-service API tests passed in the top integration branch:
  71 passed, 0 failed.
- Holiday-aware self-service component tests passed in the top integration
  branch: 7 passed, 0 failed.
- Self-service browser test discovery in the top integration branch found 14
  lifecycle tests, including the special-day label flow.
- Web build passed.
- Frontend lint passes on the current stack with existing warnings and 0 errors.

Validated integration:

- Confirm special days appear under the space `Self-service` settings tab.
- Confirm the first customer-facing version only labels/warns for holidays until
  explicit self-service holiday policies are implemented.
- A holiday-aware browser lifecycle test has been added for member picker
  special-day labels.

Remaining manual/product check:

- Run the holiday-aware self-service browser flow against a live seeded web/API
  stack.

### `feat/self-service-portable-integration`

Purpose: integrate organization/tenant portability, contact protection, and
export plus import-validation boundaries on top of self-service and holiday
calendars.

PR:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-portable-integration

PR summary:

[Portable integration PR summary](PULL_REQUEST_SELF_SERVICE_PORTABLE_INTEGRATION.md)

Known verification:

- API build passed.
- Organization portability tests passed: 17 passed, 0 failed.
- Focused portability/self-service/special-day tests passed: 204 passed, 0 failed.
- Focused API tests passed: 223 passed, 0 failed.
- Full API suite passed: 1,947 passed, 12 skipped, 0 failed.
- Web build passed.
- Frontend lint passed with 89 existing warnings and 0 errors.
- Self-service browser test discovery found 13 lifecycle tests.

Preserved self-service files:

- `SpecialLeaveRequestsController`
- `SpecialLeaveRequestCommands`
- `SpecialLeaveRequestQueries`
- `SpecialLeaveDtos`
- `SpecialLeaveRequest`

Validated integration:

- Organization export manifests, packages, and dry-run import validation counts
  now include manual self-service workflow records listed in the portability
  contract.

Remaining manual/product check:

- Smoke-test customer-hosted setup with real secrets, especially
  `FIELD_ENCRYPTION_KEY`.
- Smoke-test member/admin self-service flows after migrations run on a real
  database.

### `feat/self-service-portability-export-readiness`

Purpose: top integration branch for export-ready manual self-service state and
holiday-aware self-service cycle context.

PR:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-portability-export-readiness

PR summary:

[Portability export readiness PR summary](PULL_REQUEST_SELF_SERVICE_PORTABILITY_EXPORT_READINESS.md)

Known verification:

- API build passed.
- Organization portability tests passed: 17 passed, 0 failed.
- Focused portability/self-service/special-day tests passed: 204 passed, 0 failed.
- Focused slot/status holiday-awareness tests passed: 71 passed, 0 failed.
- Full API suite passed: 1,949 passed, 12 skipped, 0 failed.
- Web build passed.
- Frontend lint passed with 89 existing warnings and 0 errors.
- Holiday-aware self-service component tests passed: 7 passed, 0 failed.
- Self-service browser test discovery found 14 lifecycle tests, including the
  special-day label flow.

Validated integration:

- Organization export manifests, packages, and dry-run import validation counts
  include manual self-service workflow records.
- Member-facing self-service slots show marked special days.
- Admin cycle status exposes special-day counts and labels underfilled slots on
  marked special days.
- A browser lifecycle flow now creates a special day for a real seeded slot and
  verifies the member picker renders the label.

Remaining manual/product check:

- Run the holiday-aware browser lifecycle flow against a live seeded web/API
  stack.
- Smoke-test customer-hosted setup with real secrets, especially
  `FIELD_ENCRYPTION_KEY`.
- Smoke-test member/admin self-service flows after migrations run on a real
  database.

### `feat/self-service-client-ready`

Purpose: umbrella branch for manual self-service, holiday calendars, portable
isolation, export readiness, and client-hosted packaging.

PR:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-client-ready

PR summary:

[Client-ready PR summary](PULL_REQUEST_SELF_SERVICE_CLIENT_READY.md)

Known verification:

- Inherits all verification from `feat/self-service-portability-export-readiness`.
- Adds compose/env wiring for `FIELD_ENCRYPTION_KEY`.
- Adds customer env validation for `FIELD_ENCRYPTION_KEY` length and placeholder
  replacement.
- Adds customer-hosted entitlement validation for `SHIFTER_LICENSEE` and
  `SHIFTER_LICENSE_KEY` in both install validators and API startup.
- Supports signed offline license files for private-network installs, verified
  with an RSA public key at API startup.
- Adds a signed license generator script for issuing customer license JSON
  files from an OfekLabs-owned RSA private key.
- Adds a customer env validator harness that checks valid envs, no-export AI
  rejection/allowance, and short field encryption keys against the PowerShell
  validator and Git Bash validator when available. It also asserts warning
  output when optional external processors are configured for customer-hosted
  installs.
- The same env validator harness accepts complete signed offline license-file
  configuration and rejects partial signed-license configuration.
- Customer env validators fail partial optional provider groups for Resend,
  Twilio, Web Push VAPID, Pushover, and LemonSqueezy before deployment.
- Adds `infra/scripts/smoke-self-service-client-ready.ps1` for live seeded stack
  preflight, member/admin self-service workflow read-model checks, admin
  assignment/closeout checks, plus the holiday/special-day picker browser flow.
- Adds `infra/scripts/seed-compose.sh` to load the seeded demo dataset into a
  running customer/local Compose PostgreSQL service before live smoke tests,
  with a dry-run harness for target resolution.
- Adds `infra/scripts/bundle-compose-images.sh` for restricted-network customer
  installs, producing a Docker image tarball plus manifest and checksum from
  the customer Compose file.
- The live self-service smoke can now read target URLs and optional seeded demo
  credentials from the customer env file with `-EnvFile`, and supports
  `-ResolveOnly` for dry configuration checks before live services are ready.
- The smoke script also checks `infra/scripts/restore-compose.sh` syntax when
  Bash/Git Bash is available.
- Adds `infra/scripts/restore-compose.sh` and customer-hosted restore runbook
  docs for PostgreSQL dumps and optional uploads-volume archives.
- Ensures `restore-compose.sh` passes the configured `ENV_FILE` through to
  Compose and adds a dry-run harness for that path.
- Ensures backup, deploy, and restore compose scripts all pass the configured
  `ENV_FILE` to Docker Compose.
- Adds a backup compose harness with a fake Docker shim to verify database dump
  creation and custom env/project propagation without touching real Docker.
- Adds a deploy compose harness with fake Git, Docker, and Curl shims to verify
  deploy orchestration, pre-deploy backup, custom env/project propagation, and
  web/API health checks without touching real services.
- Adds a deploy rollback harness with fake Git, Docker, and Curl shims to
  verify failed deploy health checks roll back to the previous git revision and
  re-check web/API health.
- Adds a customer-hosted package preflight that runs the env, restore, backup,
  deploy, rollback, script syntax, and Compose config checks as one command.
- Adds a customer-hosted handoff notes template for package checksum, license,
  domains, env/secrets ownership, provider approvals, verification evidence,
  backup/restore, migration, security, and escalation sign-off, and includes it
  in the generated customer package.
- The customer-hosted package preflight can now target a real customer env file
  with `-EnvFile ... -ValidateEnvFile`, so placeholder-free customer secrets and
  domains are checked before install.
- Adds `infra/scripts/verify-customer-hosted-install.ps1` as the target-host
  wrapper for the real customer env preflight, seed setup, and live smoke
  sequence.
- Adds `/ready` as the API readiness endpoint for Compose, deployment scripts,
  ECS, and future orchestrators.
- Adds package reference validation for exported users, owner/member links, core
  scheduling rows, and self-service workflow relationships.
- Adds a conservative organization package import executor for safe packages,
  with explicit confirmation, dependency-ordered transactional writes, and
  migrated PostgreSQL compatibility fields for legacy self-service columns.
- Strengthens special-leave query isolation coverage across space boundaries.
- Adds organization-level self-service default templates for multi-space
  customers. First-time self-service group policy resolves from space defaults,
  then organization defaults, then install env defaults, and organization
  templates are included in export/import package validation.
- Adds a Platform organization self-service defaults panel so platform admins
  can edit multi-space customer policy templates without calling the API
  directly.
- Clarifies the holiday/manual self-service boundary: special-day labels,
  cycle counts, and underfilled-slot warnings are covered now; holiday-specific
  staffing policy changes remain future scope.
- Adds the first holiday/special-day policy behavior for manual self-service:
  `requiresCoverage=false` special days block member picks, waitlist joins,
  waitlist offer cascades, and stale waitlist offer acceptance while keeping the
  slots visible with no-coverage labels.
- Extends the same policy to special leave: no-coverage special-day overlaps are
  rejected, and coverage-required special-day overlaps are highlighted in admin
  notifications.
- Adds special-day impact to cycle closeout and exports: total special-day
  slots, no-coverage special-day slots, and underfilled special-day slots.
- Exposes member workflow policy flags in the My Shifts response and hides
  member change, absence, and swap actions when the group policy disables them.
- Exposes pick/waitlist policy flags in the Available Slots response and shows
  disabled policy labels instead of pick or waitlist buttons.
- Ends sessions after password changes and redirects users back to login with a
  success notice.
- Supports PWA install prompts on mobile and desktop when the browser reports
  install availability, uses device-neutral copy, and caches/refreshes
  self-service member read models for installed/offline usage.
- Adds installed-PWA shortcuts for personal shifts, self-service shift picking,
  and profile/account tools.
- Refreshes member-safe self-service cache entries on reconnect for the current
  group and last picked self-service group, while avoiding admin-only closeout
  refreshes for normal members.
- Keeps Sentry disabled unless `NEXT_PUBLIC_SENTRY_DSN` is explicitly
  configured, so customer-hosted installs can leave error tracking off by
  default.
- Keeps PostHog disabled unless `NEXT_PUBLIC_POSTHOG_KEY` is explicitly
  configured in production, including direct analytics calls.
- Keeps Crisp disabled unless `NEXT_PUBLIC_CRISP_WEBSITE_ID` is explicitly
  configured, trimming accidental whitespace before loading the widget.
- Routes assistant, landing, profile feedback, and accessibility contact entry
  points through `NEXT_PUBLIC_LEGAL_EMAIL`, with a fallback support mailbox only
  when the public legal/support email is not configured.
- Enforces `AI_NO_EXPORT_REQUIRED=true` in both customer env validation and API
  startup, rejecting public hosted AI endpoints for no-export installs, and
  validates private AI endpoint URL/model requirements at startup.
- Provider health checks treat unconfigured LemonSqueezy billing as skipped,
  report partial billing configuration with exact missing keys, and expose
  core/optional provider readiness in the platform UI.
- Adds a Cloudflare edge security baseline for DNS/TLS, first WAF/rate-limit
  rules, API/PWA caching, and customer-hosted WAF decisions.
- Live client-ready smoke passed against a fresh SQL install from all
  migrations plus `seed.sql`, a live API, and a rebuilt production web server:
  `infra/scripts/smoke-self-service-client-ready.ps1 -ApiBaseUrl http://localhost:5015 -WebBaseUrl http://localhost:3015`.
  This covered restore script syntax, seeded demo users, the self-service demo
  cycle, member/admin workflow read models, available-slot/member/closeout
  workflow policy flags, admin assignment reads, seeded special-day closeout
  impact counts, closeout CSV archive policy/special-day rows, closeout PDF
  download checks, web reachability, and the
  Playwright special-day picker browser flow.
- Live smoke env-file resolution passed:
  `infra/scripts/smoke-self-service-client-ready.ps1 -EnvFile infra/compose/.env.customer.example -ResolveOnly`.
- Targeted organization-defaults and portability tests passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~ChangeSchedulingModeCommandTests|FullyQualifiedName~OrganizationPortabilityTests"`.
- Organization portability coverage now exports, validates, and imports the full
  manual self-service graph into a clean target context, including defaults,
  special days, cycles, slots, requests, attendance, absences, change requests,
  waitlists, swaps, special leave, notifications, and audit logs.
- Organization import controller route coverage passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~PlatformControllerImportTests`.
- AI no-export guard, assistant fallback, and AI health checks passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~AiConfigurationGuardTests|FullyQualifiedName~AiAssistantSupportTests|FullyQualifiedName~AiHealthCheckTests"`.
- Customer env validator harness passed:
  `infra/scripts/test-customer-env-validator.ps1`.
- Restore dry-run harness passed:
  `infra/scripts/test-restore-compose-dry-run.ps1`.
- Seed compose dry-run harness passed:
  `infra/scripts/test-seed-compose-dry-run.ps1`.
- Signed license generator harness passed:
  `infra/scripts/test-generate-signed-license.ps1`.
- Offline image bundle harness passed:
  `infra/scripts/test-bundle-compose-images.ps1`.
- Customer-hosted install wrapper dry-run passed:
  `infra/scripts/verify-customer-hosted-install.ps1 -EnvFile infra/compose/.env.customer.example -SkipPackagePreflight -SeedDryRun -ResolveOnly`.
- Customer-hosted install wrapper harness passed:
  `infra/scripts/test-verify-customer-hosted-install-dry-run.ps1`.
- Backup compose harness passed:
  `infra/scripts/test-backup-compose.ps1`.
- Deploy compose harness passed:
  `infra/scripts/test-deploy-compose.ps1`.
- Deploy compose rollback harness passed:
  `infra/scripts/test-deploy-compose-rollback.ps1`.
- Customer-hosted package preflight passed:
  `infra/scripts/test-customer-hosted-package.ps1`.
- Customer-hosted package assembly now creates a zip plus `.sha256` checksum,
  extracts the archive in the test harness, validates the extracted Compose
  config, blocks obvious private env/license/key material, and dry-runs the
  packaged install verifier from the extracted directory.
- Customer-hosted package assembly now requires
  `docs/CUSTOMER-HOSTED-HANDOFF-NOTES.md`, so the operational handoff checklist
  ships with the package and is asserted by the extracted-package test.
- Explicit-env customer-hosted package preflight passed against the checked-in
  customer template path:
  `infra/scripts/test-customer-hosted-package.ps1 -EnvFile infra/compose/.env.customer.example`.
- Customer-hosted package preflight is CI-backed by
  `.github/workflows/customer-hosted-preflight.yml` on relevant PRs, pushes to
  `feat/self-service-client-ready`, and manual dispatch.
- The workflow trigger watches the package-relevant API, web, solver,
  Docker/Compose/migrations/scripts, and customer handoff docs so package
  drift is checked when any shipped surface changes.
- The branch preflight workflow has passed on package-relevant commits; see the
  workflow history:
  https://github.com/OfekItzhaki/Shifter/actions/workflows/customer-hosted-preflight.yml
- PostgreSQL organization import smoke passed:
  `infra\\scripts\\smoke-organization-import-postgres.ps1`, which starts a
  temporary PostgreSQL 16 container, applies all SQL migrations, and imports a
  validated organization package into the migrated target.
- Self-service scope and lifecycle tests passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~SelfServiceScopeTests|FullyQualifiedName~ManualSelfServiceLifecycleTests"`.
  This includes disabled workflow policy coverage for shift-change request
  submission.
- Focused no-coverage special-day policy tests passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~SlotAvailabilityEngineTests|FullyQualifiedName~ManualSelfServiceLifecycleTests|FullyQualifiedName~WaitlistServiceTests"`.
  This includes picker read-model labels, member claim rejection, waitlist join
  rejection, waitlist cascade suppression, and stale waitlist offer rejection.
- Focused no-coverage picker test and lint passed:
  `node_modules\\.bin\\vitest.cmd run __tests__\\selfService\\slotBrowserTab.test.tsx`
  and
  `node_modules\\.bin\\eslint.cmd app\\groups\\[groupId]\\tabs\\SlotBrowserTab.tsx lib\\api\\selfService.ts __tests__\\selfService\\slotBrowserTab.test.tsx`.
  This includes no-coverage special-day and disabled pick/waitlist policy
  states.
- Focused Available Slots policy flag API test passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~SelfServiceScopeTests.GetAvailable_AllowsGroupMemberWithoutSpaceViewGrant`.
- Focused special leave special-day policy tests passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~SpecialLeaveRequestCommandTests`.
  This includes rejecting no-coverage special-day overlap and highlighting
  coverage-required special-day overlap to admins.
- Focused special-day closeout/export tests passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~SelfServiceScopeTests.GetCycleCloseout|FullyQualifiedName~SelfServiceScopeTests.ExportCloseout"`.
  This includes closeout, CSV, and PDF special-day impact metrics.
- Focused closeout archive verification passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~SelfServiceScopeTests.ExportCloseout`.
  This includes proof that the closeout PDF verification fingerprint matches
  the SHA-256 of the exported closeout CSV bytes.
- Focused operations closeout UI test and lint passed:
  `node_modules\\.bin\\vitest.cmd run __tests__\\selfService\\selfServiceOperationsTab.test.tsx`
  and
  `node_modules\\.bin\\eslint.cmd components\\groups\\selfService\\SelfServiceOperationsTab.tsx lib\\api\\selfService.ts __tests__\\selfService\\selfServiceOperationsTab.test.tsx`.
- Focused My Shifts workflow policy tests and lint passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~SelfServiceScopeTests.ListMine_CurrentShiftCount_CountsOnlyApprovedAssignments`,
  `node_modules\\.bin\\vitest.cmd run __tests__\\selfService\\myShiftsTab.test.tsx`,
  and
  `node_modules\\.bin\\eslint.cmd components\\groups\\selfService\\MyShiftsTab.tsx lib\\api\\selfService.ts __tests__\\selfService\\myShiftsTab.test.tsx`.
- Browser lifecycle discovery passed with 15 tests, and the suite now includes
  slot fill-count final-state coverage for picks, waitlist join/leave, shift
  cancellation, shift-change approval/rejection, swap accept/decline/cancel,
  and cannot-attend rejection.
- Customer-hosted package assembly preflight passed:
  `infra\\scripts\\test-package-customer-hosted.ps1`.
  This now renders the extracted package Compose config with non-default
  `SELF_SERVICE_DEFAULT_*` probe values and asserts those exact values appear
  in the API's `SelfServiceDefaults__*` environment bindings.
- Backup, deploy, restore, and seed compose script syntax checks passed after
  custom `ENV_FILE` propagation.
- Resend sender and health check coverage passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~ResendHealthCheckTests|FullyQualifiedName~ResendEmailSenderTests"`.
- Provider health check coverage passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~HealthChecks`.
- Health endpoint coverage passed after adding `/ready`:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~HealthEndpointIntegrationTests`.
- Detailed health and AI mode metadata coverage passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~HealthEndpointIntegrationTests|FullyQualifiedName~AiHealthCheckTests"`.
  This includes preserving provider metadata in `/health/detailed` so
  customer-hosted no-export/private AI mode can be verified without exposing
  secrets.
- Install-level self-service default validation passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~SelfServiceDefaultPolicyOptionsTests|FullyQualifiedName~ChangeSchedulingModeCommandTests"`.
  This includes startup-validation coverage for customer install defaults
  before they can leak into new self-service group policy.
- Fresh SQL install from all `infra/migrations/*.sql` plus `seed.sql` passed
  after adding `086_organization_self_service_defaults.sql`.
- PWA prompt regression tests passed:
  `node_modules\\.bin\\vitest.cmd run __tests__\\shell\\pwaInstallPrompt.test.tsx`.
- PWA self-service cache regression tests and service worker syntax checks
  passed:
  `node_modules\\.bin\\vitest.cmd run __tests__\\cache\\backgroundRefresh.test.ts __tests__\\cache\\cacheLifecycle.test.ts`
  and `node --check public\\sw.js`.
- PWA manifest shortcut tests passed for install metadata plus
  `/schedule/my-missions`, `/pick`, and `/profile` with existing icon assets:
  `node_modules\\.bin\\vitest.cmd run __tests__\\pwa\\manifest.test.ts`.
- Landing PWA copy regression tests passed, confirming phone and desktop install
  support copy does not drift back to phone-only wording:
  `node_modules\\.bin\\vitest.cmd run __tests__\\landing\\landingContent.test.ts`.
- Focused PWA prompt ESLint and `node_modules\\.bin\\next.cmd build` passed.
- Sentry config tests and focused ESLint passed:
  `node_modules\\.bin\\vitest.cmd run __tests__\\monitoring\\sentryConfig.test.ts`
  and
  `node_modules\\.bin\\eslint.cmd sentry.client.config.ts sentry.server.config.ts lib\\monitoring\\sentryConfig.ts __tests__\\monitoring\\sentryConfig.test.ts`.
- PostHog config tests and focused ESLint passed:
  `node_modules\\.bin\\vitest.cmd run __tests__\\monitoring\\sentryConfig.test.ts __tests__\\monitoring\\posthogConfig.test.ts`
  and
  `node_modules\\.bin\\eslint.cmd lib\\analytics\\posthog.ts lib\\analytics\\posthogConfig.ts __tests__\\monitoring\\posthogConfig.test.ts`.
- Crisp config tests and focused ESLint passed:
  `node_modules\\.bin\\vitest.cmd run __tests__\\monitoring\\sentryConfig.test.ts __tests__\\monitoring\\posthogConfig.test.ts __tests__\\monitoring\\crispConfig.test.ts`
  and
  `node_modules\\.bin\\eslint.cmd app\\layout.tsx lib\\support\\crispConfig.ts __tests__\\monitoring\\crispConfig.test.ts`.
- Support contact routing tests passed:
  `node_modules\\.bin\\vitest.cmd run __tests__\\support\\contact.test.ts`.

Remaining manual/product check:

- On the target host, run the customer-hosted install verifier against the real
  customer env file after secrets/domains are set and the stack is running:
  `infra/scripts/verify-customer-hosted-install.ps1 -EnvFile infra/compose/.env`.
- Before a production tenant-by-tenant migration, rerun the organization import
  smoke against that customer's target PostgreSQL; full deployment moves should
  still use the compose backup/restore flow.

## PR Opening Notes

PR `#36` already merged the client-ready stack into `develop`.

Do not open or merge a `develop` to `main` PR until the release readiness gate
passes against the current `develop` head:

```powershell
infra/scripts/check-release-readiness.ps1 `
  -RequireDedicatedStagingSecrets `
  -SkipHostedSmoke
```

That gate currently requires staging URL variables and a successful staging
deploy for the exact candidate commit. For the final `develop` to `main` gate,
it also requires dedicated `STAGING_*` SSH secrets instead of production-named
fallback secrets. After staging deploy succeeds, complete
`docs/STAGING-MANUAL-SMOKE-EVIDENCE.md`, verify it with
`infra/scripts/check-staging-smoke-evidence.ps1`, and run the hosted smoke
against the deployed staging or production URLs before moving to `main`.
