# Develop To Main Release Gate

Use this checklist before opening or merging a `develop` to `main` PR.

## Required Evidence

- `develop` is clean and pushed.
- Customer-hosted preflight is green for the latest code-changing commit.
- Broad CI is green for the latest code-changing commit.
- Staging is deployed from `develop`.
- Release readiness audit has no failures:

  ```powershell
  .\infra\scripts\check-release-readiness.ps1 -SkipHostedSmoke
  ```

- GitHub staging setup was applied with the intended staging URLs/path:

  ```powershell
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

- `develop` has green CI evidence.
- Customer-hosted package verification has passed locally and in GitHub Actions.
- The production-hosted API `/health` is healthy, but `/ready` returns 404,
  which means the hosted API has not yet been deployed to the readiness-probe
  build.
- The release readiness audit script is in place and its harness passes.
- The GitHub staging setup helper is in place and dry-run/apply behavior is
  covered by a local harness.
- The staging manual smoke evidence template is in place, but no real staging
  user-flow sign-off has been recorded yet.
- The real release readiness audit currently fails because the GitHub
  `staging` environment and staging URL/path repository variables are not
  configured yet. This is the expected blocker before a `develop` to `main` PR.
- The `Deploy Staging` workflow exists, but staging variables and the GitHub
  `staging` environment are not configured yet. It can use existing `VPS_*`
  secrets as a fallback, but dedicated `STAGING_*` secrets are preferred.

Do not open the final `develop` to `main` PR until the staging deploy and manual
smoke evidence above are complete.
