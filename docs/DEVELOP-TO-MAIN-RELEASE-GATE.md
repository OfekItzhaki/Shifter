# Develop To Main Release Gate

Use this checklist before opening or merging a `develop` to `main` PR.

## Required Evidence

- `develop` is clean and pushed.
- Customer-hosted preflight is green for the latest code-changing commit.
- Broad CI is green for the latest code-changing commit.
- GitHub release controls pass. The release readiness audit below now checks
  these controls directly; this standalone command is useful when you only want
  to inspect branch protection:

  ```powershell
  .\infra\scripts\check-github-release-controls.ps1
  ```

- To configure the expected controls, dry-run first and then apply:

  ```powershell
  .\infra\scripts\setup-github-release-controls.ps1

  .\infra\scripts\setup-github-release-controls.ps1 -Apply
  ```

  This updates or creates active rulesets so `main` blocks deletion/force-push,
  requires PRs, and requires `API Build & Test`, `Frontend Build`,
  `Solver Lint & Test`, and `Package Preflight`; it also protects `develop`
  from deletion and force-push.

- Staging is deployed from `develop`.
- Release readiness audit has no failures, including staging setup, dedicated
  staging SSH secrets, latest CI evidence, customer-hosted preflight evidence,
  latest successful staging deploy, and GitHub release controls:

  ```powershell
  .\infra\scripts\check-release-readiness.ps1 `
    -RequireDedicatedStagingSecrets `
    -SkipHostedSmoke
  ```

- GitHub staging setup was applied with the intended staging URLs/path:

  ```powershell
  # Safe partial setup before staging URLs exist.
  .\infra\scripts\setup-github-staging.ps1 -BootstrapOnly -Apply

  # Full setup once staging URLs are allocated.
  .\infra\scripts\setup-github-staging.ps1 `
    -WebBaseUrl <staging-web-url> `
    -ApiBaseUrl <staging-api-url> `
    -StagingPath /opt/shifter-staging `
    -ComposeProjectName shifter-staging `
    -Apply
  ```

- Staging hosted smoke passes:

  ```powershell
  .\infra\scripts\smoke-hosted-vps.ps1 `
    -WebBaseUrl <staging-web-url> `
    -ApiBaseUrl <staging-api-url>
  ```

- Manual staging smoke passes:
  - use [Staging manual smoke evidence](STAGING-MANUAL-SMOKE-EVIDENCE.md)
  - validate the completed evidence file:

    ```powershell
    .\infra\scripts\check-staging-smoke-evidence.ps1 `
      -EvidencePath <completed-staging-smoke-evidence.md>
    ```

  - login
  - registration or invite acceptance
  - forgot-password and reset-password
  - change password
  - PWA install on at least one desktop or mobile browser
  - pick an open self-service shift
  - join and leave a waitlist
  - cancel a shift
  - report cannot attend
  - approve and reject absence reports
  - request, approve, and reject shift changes
  - propose, accept, decline, and cancel swaps
  - request, cancel, approve, and reject special leave
  - export closeout CSV and PDF

## Current Status

As of June 13, 2026:

- `develop` is the current integration branch and has green current-head CI
  evidence for commit `fd9481b`
  (`test(release): require current-head ci evidence`): broad `CI` run
  `27454705062` passed and `Customer-Hosted Preflight` run `27454705066`
  passed. `Deploy Staging` run `27454705063` skipped as expected while staging
  deploy is disabled.
- Local verification against a fresh migrated/seeded PostgreSQL database passed
  for the current `develop` head:
  - `dotnet build apps\api\Jobuler.Api\Jobuler.Api.csproj`
  - `npm run lint` from `apps/web` with 0 errors and existing warnings
  - `npx playwright test e2e/self-service.browser.spec.ts --reporter=line`
    from `apps/web`: 15 passed
  - `infra/scripts/smoke-self-service-client-ready.ps1` against local API/web:
    passed
  - `infra/scripts/smoke-hosted-vps.ps1` against local API/web: passed after
    decoding byte-array manifest/service-worker responses from PowerShell
- The broad `CI` workflow now runs on pushes to `develop` and PRs into
  `develop` or `main`, so release PR status checks can be enforced instead of
  relying only on manual dispatch.
- `Customer-Hosted Preflight` now runs on every push to `develop` and every PR
  into `develop` or `main`; it deliberately has no path filter because
  `Package Preflight` is a required release status check.
- Customer-hosted package verification has passed locally and in GitHub Actions.
- The historical feature branches are stale stack tips after the PR `#36`
  squash merge and later `develop` hardening. Do not merge those branch tips
  directly into `develop`; review and cherry-pick only a specific missing patch
  if a later audit proves one is absent from current `develop`.
- The production-hosted API `/health` is healthy, but `/ready` returns 404,
  which means the hosted API has not yet been deployed to the readiness-probe
  build.
- The release readiness audit script is in place and its harness passes. It now
  requires successful broad `CI` and `Customer-Hosted Preflight` runs for the
  current candidate HEAD, includes the GitHub release-control audit so the
  `develop` to `main` gate fails if `main` does not require PRs/status checks,
  and fails until a successful `Deploy Staging` run exists for the current
  `develop` HEAD.
- The GitHub staging setup helper is in place and dry-run/apply behavior is
  covered by a local harness. It also supports `-BootstrapOnly` to create the
  `staging` environment and safe non-URL defaults before staging DNS/URLs are
  ready.
- The staging manual smoke evidence template is in place, but no real staging
  user-flow sign-off has been recorded yet.
- The GitHub release-control audit is in place and currently passes: `main`
  blocks deletion/force-push, requires pull requests, and requires the expected
  status checks; `develop` blocks deletion/force-push. The setup helper is in
  place and dry-run/apply behavior is covered by a local harness.
- The GitHub `staging` environment is created, and staging path/project
  variables are configured with push deploy disabled. The real strict release
  readiness audit currently fails because `STAGING_WEB_BASE_URL`,
  `STAGING_API_BASE_URL`, and dedicated `STAGING_*` SSH secrets are not
  configured yet, and because no successful staging deploy exists for the
  current `develop` head. This is the expected blocker before a staging deploy
  and `develop` to `main` PR.
- The `Deploy Staging` workflow exists, but staging variables and the GitHub
  URLs are not configured yet. It can use existing `VPS_*` secrets as a
  fallback, but dedicated `STAGING_*` secrets are preferred.

Do not open the final `develop` to `main` PR until the staging deploy and manual
smoke evidence above are complete.
