# Staging Environment Setup

Use this runbook to create a persistent staging environment for Shifter on the
current VPS.

The goal is to test `develop` at real hosted URLs before merging `develop` into
`main` and deploying production.

## Target Topology

| Environment | Branch | Path | URLs |
| --- | --- | --- | --- |
| Production | `main` | `/opt/shifter` | `https://shifter.ofeklabs.com` |
| Staging | `develop` | `/opt/shifter-staging` | `https://staging.shifter.ofeklabs.com`, `https://staging-api.shifter.ofeklabs.com` |

Staging should use separate Compose project names, ports, volumes, passwords,
JWT secrets, storage credentials, email credentials, and billing/test-provider
credentials.

Do not point staging at the production database, Redis instance, object storage
bucket, live billing keys, production email sender, or production messaging
sender.

## Before You Start

- Confirm the VPS has enough CPU, RAM, and disk for a second stack.
- Keep production running from `/opt/shifter`.
- Keep staging isolated under `/opt/shifter-staging`.
- Protect staging with Basic Auth unless it is intentionally public.
- Keep `ENABLE_STAGING_DEPLOY=false` in GitHub until the first manual staging
  deploy and smoke check pass.

If the VPS is small, staging can affect production performance because it runs a
second web app, API, database, Redis, solver, MinIO, and monitoring stack.

## Step 1: Add DNS Records

In the DNS provider for `ofeklabs.com`, add:

```text
staging.shifter.ofeklabs.com      A    <your-vps-ip>
staging-api.shifter.ofeklabs.com  A    <your-vps-ip>
```

Wait until both names resolve to the VPS:

```powershell
Resolve-DnsName staging.shifter.ofeklabs.com
Resolve-DnsName staging-api.shifter.ofeklabs.com
```

## Step 2: Clone A Separate Staging Working Copy

SSH into the VPS and clone the repo into a separate staging folder:

```bash
cd /opt
git clone https://github.com/OfekItzhaki/Shifter.git shifter-staging
cd /opt/shifter-staging
git checkout develop
git pull --ff-only origin develop
```

Production should remain in its existing folder, usually `/opt/shifter`.

Alternatively, after DNS is ready, the GitHub `Deploy Staging` workflow can now
bootstrap `/opt/shifter-staging` automatically if it does not exist yet. Keep
`ENABLE_STAGING_DEPLOY=false` and run the workflow manually from `develop` for
the first staging deploy.

## Step 3: Create The Staging Env File

On the VPS:

```bash
cd /opt/shifter-staging/infra/compose
cp .env.staging.example .env
```

Edit `/opt/shifter-staging/infra/compose/.env`.

Set the staging domains:

```text
APP_FRONTEND_BASE_URL=https://staging.shifter.ofeklabs.com
APP_API_BASE_URL=https://staging-api.shifter.ofeklabs.com
NEXT_PUBLIC_API_URL=https://staging-api.shifter.ofeklabs.com
```

Keep the staging project and ports separate from production:

```text
COMPOSE_PROJECT_NAME=shifter-staging
POSTGRES_PORT=15432
REDIS_PORT=16379
API_PORT=15000
SOLVER_PORT=18000
WEB_PORT=13000
MINIO_PORT=19000
MINIO_CONSOLE_PORT=19001
```

Replace every placeholder secret in `.env`, especially:

```text
POSTGRES_PASSWORD
REDIS_PASSWORD
JWT_SECRET
FIELD_ENCRYPTION_KEY
MINIO_ROOT_PASSWORD
SEQ_ADMIN_PASSWORD
SEQ_UI_PASSWORD_HASH
```

Use staging/sandbox provider credentials where possible:

```text
RESEND_API_KEY
RESEND_FROM_EMAIL
TWILIO_ACCOUNT_SID
TWILIO_AUTH_TOKEN
LEMONSQUEEZY_API_KEY
LEMONSQUEEZY_WEBHOOK_SECRET
STORAGE_S3_*
VAPID_*
AI_API_KEY
```

Leave optional providers empty if you are not testing them yet.

You can use the bootstrap helper to create or refresh the staging repo/env
without overwriting an existing `.env` file:

```bash
chmod +x /opt/shifter-staging/infra/scripts/bootstrap-staging.sh
WEB_BASE_URL=https://staging.shifter.ofeklabs.com \
API_BASE_URL=https://staging-api.shifter.ofeklabs.com \
SHIFTER_DIR=/opt/shifter-staging \
COMPOSE_PROJECT_NAME=shifter-staging \
/opt/shifter-staging/infra/scripts/bootstrap-staging.sh
```

To also add managed Caddy staging blocks, pass `APPLY_CADDY=true` and a Basic
Auth password:

```bash
chmod +x /opt/shifter-staging/infra/scripts/bootstrap-staging.sh
WEB_BASE_URL=https://staging.shifter.ofeklabs.com \
API_BASE_URL=https://staging-api.shifter.ofeklabs.com \
BASIC_AUTH_USERNAME=admin \
BASIC_AUTH_PASSWORD='<choose-a-strong-password>' \
APPLY_CADDY=true \
SHIFTER_DIR=/opt/shifter-staging \
COMPOSE_PROJECT_NAME=shifter-staging \
/opt/shifter-staging/infra/scripts/bootstrap-staging.sh
```

## Step 4: Add Caddy Routes

If Caddy is installed on the VPS as a system service, edit:

```bash
sudo nano /etc/caddy/Caddyfile
```

Add staging routes:

```caddy
staging.shifter.ofeklabs.com {
    header {
        X-Frame-Options "DENY"
        X-Content-Type-Options "nosniff"
        Referrer-Policy "no-referrer"
        -Server
        -X-Powered-By
    }

    basicauth {
        admin <bcrypt-password-hash>
    }

    reverse_proxy localhost:13000
}

staging-api.shifter.ofeklabs.com {
    basicauth {
        admin <bcrypt-password-hash>
    }

    reverse_proxy localhost:15000
}
```

Generate the Basic Auth password hash:

```bash
docker run --rm caddy:2-alpine caddy hash-password --plaintext 'choose-a-strong-password'
```

Paste the generated hash in place of `<bcrypt-password-hash>`.

Validate and reload Caddy:

```bash
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

If production Caddy is containerized instead of installed as a service, apply
the same route blocks to the Caddy configuration used by that container and
reload the containerized Caddy instance.

## Step 5: Start The Staging Stack

On the VPS:

```bash
cd /opt/shifter-staging/infra/compose
docker compose --project-name shifter-staging up -d --build
```

Check container status:

```bash
docker compose --project-name shifter-staging ps
```

Check logs if something is not healthy:

```bash
docker compose --project-name shifter-staging logs --tail=200
```

## Step 6: Run Hosted Smoke Checks

From your local machine:

```powershell
.\infra\scripts\smoke-hosted-vps.ps1 `
  -WebBaseUrl https://staging.shifter.ofeklabs.com `
  -ApiBaseUrl https://staging-api.shifter.ofeklabs.com `
  -BasicAuthUsername admin `
  -BasicAuthPassword <basic-auth-password>
```

This checks the staging API readiness/health, frontend landing page, auth pages,
PWA manifest/icon, and service worker.

## Step 7: Configure GitHub Staging Variables

From your local repo, dry-run the GitHub setup first:

```powershell
.\infra\scripts\setup-github-staging.ps1 `
  -WebBaseUrl https://staging.shifter.ofeklabs.com `
  -ApiBaseUrl https://staging-api.shifter.ofeklabs.com `
  -StagingPath /opt/shifter-staging `
  -ComposeProjectName shifter-staging
```

Apply it when the preview is correct:

```powershell
.\infra\scripts\setup-github-staging.ps1 `
  -WebBaseUrl https://staging.shifter.ofeklabs.com `
  -ApiBaseUrl https://staging-api.shifter.ofeklabs.com `
  -StagingPath /opt/shifter-staging `
  -ComposeProjectName shifter-staging `
  -Apply
```

This should set:

```text
STAGING_WEB_BASE_URL=https://staging.shifter.ofeklabs.com
STAGING_API_BASE_URL=https://staging-api.shifter.ofeklabs.com
STAGING_PATH=/opt/shifter-staging
STAGING_COMPOSE_PROJECT_NAME=shifter-staging
ENABLE_STAGING_DEPLOY=false
```

Keep `ENABLE_STAGING_DEPLOY=false` until the first manual staging deploy and
manual smoke pass.

## Step 8: Add GitHub Staging Secrets

Set dedicated staging SSH secrets:

```powershell
gh secret set STAGING_HOST --repo OfekItzhaki/Shifter
gh secret set STAGING_USER --repo OfekItzhaki/Shifter
gh secret set STAGING_SSH_KEY --repo OfekItzhaki/Shifter
gh secret set STAGING_PORT --repo OfekItzhaki/Shifter
```

If staging uses Basic Auth, also set:

```powershell
gh secret set STAGING_BASIC_AUTH_USERNAME --repo OfekItzhaki/Shifter
gh secret set STAGING_BASIC_AUTH_PASSWORD --repo OfekItzhaki/Shifter
```

Use the same Basic Auth username/password that Caddy requires for the staging
URLs.

## Step 9: Run The GitHub Deploy Staging Workflow

In GitHub Actions, run `Deploy Staging` manually from the `develop` branch.

After it finishes, confirm:

- the deploy job passed
- the hosted smoke step passed
- the deployed revision matches the intended `develop` commit

Only after manual deploy works should push deploys be enabled:

```powershell
.\infra\scripts\setup-github-staging.ps1 `
  -WebBaseUrl https://staging.shifter.ofeklabs.com `
  -ApiBaseUrl https://staging-api.shifter.ofeklabs.com `
  -StagingPath /opt/shifter-staging `
  -ComposeProjectName shifter-staging `
  -EnablePushDeploy `
  -Apply
```

## Step 10: Complete Manual Staging QA

Use:

- [Staging manual smoke evidence](STAGING-MANUAL-SMOKE-EVIDENCE.md)
- [Manual self-service QA checklist](MANUAL-SELF-SERVICE-QA-CHECKLIST.md)
- [Hosted VPS MVP launch checklist](HOSTED-VPS-MVP-LAUNCH-CHECKLIST.md)

At minimum, verify:

- login
- password reset
- password change
- PWA install
- admin schedule views
- manual self-service open-shift pick-up
- waitlist join/leave
- shift cancellation
- cannot-attend report
- absence approval/rejection
- shift-change request approval/rejection
- swap proposal acceptance/rejection/cancel
- special leave request/cancel/approval/rejection
- exports, including closeout CSV/PDF

Then validate the completed evidence file:

```powershell
.\infra\scripts\check-staging-smoke-evidence.ps1 `
  -EvidencePath .\docs\STAGING-MANUAL-SMOKE-EVIDENCE.md
```

## Step 11: Run The Release Readiness Audit

From the local repo:

```powershell
.\infra\scripts\check-release-readiness.ps1 `
  -Repo OfekItzhaki/Shifter `
  -Branch develop `
  -RequireDedicatedStagingSecrets
```

The audit should pass before opening the final `develop` to `main` PR.

## Done Criteria

Staging is ready when:

- DNS resolves for both staging domains.
- Caddy serves HTTPS for both staging domains.
- Basic Auth protects staging, unless intentionally disabled.
- `/ready` and `/health` pass on the staging API.
- `smoke-hosted-vps.ps1` passes against staging.
- GitHub `Deploy Staging` passes from `develop`.
- Manual staging smoke evidence is completed and validated.
- Release readiness audit passes.

After that, open the final PR from `develop` to `main`.

## Related Docs

- [Staging and previews](../infra/STAGING_AND_PREVIEWS.md)
- [Develop to main release gate](DEVELOP-TO-MAIN-RELEASE-GATE.md)
- [Staging manual smoke evidence](STAGING-MANUAL-SMOKE-EVIDENCE.md)
- [Hosted VPS MVP launch checklist](HOSTED-VPS-MVP-LAUNCH-CHECKLIST.md)
