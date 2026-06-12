# Self-Service Branch Stack Status

Use this as the current branch-stack map for manual self-service,
holiday-calendar integration, and customer-hosted portability.

## Merge Order

Merge and test in this order:

1. `feat/manual-self-service-hardening`
2. `feat/self-service-holiday-integration`
3. `feat/self-service-portable-integration`
4. `feat/self-service-portability-export-readiness`
5. `feat/self-service-client-ready`

The order matters because manual self-service defines the member/admin workflow,
holiday calendars add scheduling semantics, and portable isolation packages the
result for customer-owned infrastructure. The final export-readiness branch
keeps a separate PR boundary for self-service export coverage and the first
holiday-aware manual self-service labels/warnings.

`feat/self-service-client-ready` is the umbrella branch when we want one PR for
the complete sellable client-ready slice instead of reviewing the stack as
separate PRs.

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
- Adds a customer env validator harness that checks valid envs, no-export AI
  rejection/allowance, and short field encryption keys against the PowerShell
  validator and Git Bash validator when available. It also asserts warning
  output when optional external processors are configured for customer-hosted
  installs.
- Customer env validators fail partial optional provider groups for Resend,
  Twilio, Web Push VAPID, Pushover, and LemonSqueezy before deployment.
- Adds `infra/scripts/smoke-self-service-client-ready.ps1` for live seeded stack
  preflight, member/admin self-service workflow read-model checks, admin
  assignment/closeout checks, plus the holiday/special-day picker browser flow.
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
- Adds package reference validation for exported users, owner/member links, core
  scheduling rows, and self-service workflow relationships.
- Adds a conservative organization package import executor for safe packages,
  with explicit confirmation and transactional writes.
- Strengthens special-leave query isolation coverage across space boundaries.
- Adds organization-level self-service default templates for multi-space
  customers. First-time self-service group policy resolves from space defaults,
  then organization defaults, then install env defaults, and organization
  templates are included in export/import package validation.
- Clarifies the holiday/manual self-service boundary: special-day labels,
  cycle counts, and underfilled-slot warnings are covered now; holiday-specific
  staffing policy changes remain future scope.
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
  cycle, member/admin workflow read models, available slots, admin assignment
  reads, cycle closeout metrics, web reachability, and the Playwright
  special-day picker browser flow.
- Targeted organization-defaults and portability tests passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~ChangeSchedulingModeCommandTests|FullyQualifiedName~OrganizationPortabilityTests"`.
- Organization import controller route coverage passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~PlatformControllerImportTests`.
- AI no-export guard, assistant fallback, and AI health checks passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~AiConfigurationGuardTests|FullyQualifiedName~AiAssistantSupportTests|FullyQualifiedName~AiHealthCheckTests"`.
- Customer env validator harness passed:
  `infra/scripts/test-customer-env-validator.ps1`.
- Restore dry-run harness passed:
  `infra/scripts/test-restore-compose-dry-run.ps1`.
- Backup compose harness passed:
  `infra/scripts/test-backup-compose.ps1`.
- Deploy compose harness passed:
  `infra/scripts/test-deploy-compose.ps1`.
- Deploy compose rollback harness passed:
  `infra/scripts/test-deploy-compose-rollback.ps1`.
- Customer-hosted package preflight passed:
  `infra/scripts/test-customer-hosted-package.ps1`.
- Self-service scope and lifecycle tests passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~SelfServiceScopeTests|FullyQualifiedName~ManualSelfServiceLifecycleTests"`.
  This includes disabled workflow policy coverage for shift-change request
  submission.
- Backup, deploy, and restore compose script syntax checks passed after custom
  `ENV_FILE` propagation.
- Resend sender and health check coverage passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~ResendHealthCheckTests|FullyQualifiedName~ResendEmailSenderTests"`.
- Provider health check coverage passed:
  `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter FullyQualifiedName~HealthChecks`.
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

Remaining manual/product check:

- Smoke-test customer-hosted setup with real customer secrets and a real
  database.
- Smoke-test organization package import against a real PostgreSQL target before
  promising tenant-by-tenant migration; full deployment moves should still use
  the compose backup/restore flow.

## PR Opening Notes

GitHub CLI is not installed in this local environment, so open the PRs using the
URLs above.

When creating the PRs, use each linked PR summary file for the title and
description. Keep the PR bases stacked in the same order as this document.
