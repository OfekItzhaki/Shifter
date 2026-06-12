# Hosted VPS MVP Launch Checklist

Use this checklist when OfekLabs runs Shifter as a hosted service on the
OfekLabs VPS. This is different from a customer-hosted install, where Shifter
runs inside a customer's infrastructure.

## What This Is

The hosted VPS path is the right path for:

- early production pilots
- small real user groups
- OfekLabs-managed support, backups, domains, and provider accounts
- users accessing `shifter` as a normal hosted web/PWA service

This path does not require the customer-hosted package, offline license files,
private customer AI, or a customer-owned database.

## Before Inviting Real Users

- Deploy `develop` to staging and complete the staging smoke/manual checks
  before opening the final `develop` to `main` PR.
- Deploy the intended branch to the production VPS.
- Run database migrations against the production database.
- Confirm the production domain uses HTTPS.
- Confirm `/ready` and `/health` return healthy results.
  - If `/health` is healthy but `/ready` returns 404, the hosted API is running
    an older build that predates the deploy readiness probe. Deploy the intended
    ref before continuing the pilot smoke.
- Run the hosted VPS read-only smoke check:

  ```powershell
  .\infra\scripts\smoke-hosted-vps.ps1 `
    -WebBaseUrl https://your-production-domain.example `
    -ApiBaseUrl https://your-api-domain.example
  ```

  If the production env file contains `APP_FRONTEND_BASE_URL` and
  `APP_API_BASE_URL`, resolve from it instead:

  ```powershell
  .\infra\scripts\smoke-hosted-vps.ps1 -EnvFile .\infra\compose\.env
  ```

- The GitHub `Deploy to VPS` workflow also runs this hosted smoke check after
  a successful VPS deployment, and uses the API `/ready` endpoint for the
  rollback gate before declaring the deployed Compose stack ready. This workflow
  is production-only and must run from `main`; deploy `develop` to staging with
  `infra/scripts/deploy-compose.sh` before opening the final `develop` to
  `main` PR.
- Confirm login, registration/invite, password reset, and change-password flows.
- Confirm Resend is configured for production email if email flows are enabled.
- Confirm AI mode:
  - hosted provider is allowed for the hosted service, or
  - AI is disabled until provider/privacy decisions are finalized.
- Confirm PWA install works on desktop and mobile.
- Confirm the main manual self-service flows on a pilot group:
  - pick an open shift
  - join and leave a waitlist
  - cancel a shift
  - report cannot attend
  - approve/reject absence reports
  - request/approve/reject shift changes
  - propose/accept/decline/cancel swaps
  - request/cancel/approve/reject special leave
- Confirm closeout CSV/PDF export works and the PDF fingerprint is visible.
- Confirm backups run and at least one restore dry-run has been tested.
- Confirm external monitoring watches the production domain and API health.
- Confirm Cloudflare DNS/WAF/rate-limit rules are in front of public traffic, or
  record why this is deferred for a private pilot.

## Recommended First Rollout

Start with a controlled pilot:

- 1 organization or space
- 1 to 3 groups
- known admins
- real schedules, but a small enough group that support is manageable
- clear feedback/support email
- daily backup enabled

Avoid a broad public launch until the pilot proves:

- users can onboard without manual database fixes
- admins understand self-service operations
- email/support flows work
- backups and restore checks are boring
- billing/legal/support copy is acceptable

## Customer-Hosted Is Separate

Customer-hosted readiness means Shifter can be packaged and installed in a
customer-owned environment with customer secrets, customer database ownership,
private/no-export AI options, license checks, package checksums, handoff notes,
and target-host verification.

That work is useful for enterprise or sensitive customers, but it is not a
blocker for running the OfekLabs-hosted VPS service.

For customer-hosted installs, use:

- [Customer-hosted deployment](CUSTOMER-HOSTED-DEPLOYMENT.md)
- [Customer-hosted handoff notes](CUSTOMER-HOSTED-HANDOFF-NOTES.md)
- [AI deployment modes](AI-DEPLOYMENT-MODES.md)

For VPS operations, use:

- [Availability runbook](../infra/AVAILABILITY.md)
- [Staging and previews](../infra/STAGING_AND_PREVIEWS.md)
- [Cloudflare front door plan](../infra/CLOUDFLARE_FRONT_DOOR.md)
