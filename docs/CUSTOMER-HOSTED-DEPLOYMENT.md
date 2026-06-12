# Customer-Hosted Deployment

This guide defines the supported path for installing Shifter inside a customer's
infrastructure. Use it when the customer needs their own database, storage, email
provider, AI endpoint, backups, and operational control.

## Recommended Model

Ship one product with two deployment modes:

| Mode | Owner | Best for |
|---|---|---|
| `saas` | OfekLabs | Standard hosted Shifter, fastest onboarding |
| `customer-hosted` | Customer or customer cloud account | Enterprise, public sector, hospitals, defense, strict data residency |

For customer-hosted installs, customer data must stay in the customer's
environment unless the customer explicitly configures an external processor such
as Resend, Twilio, LemonSqueezy, OpenAI, Sentry, PostHog, or Crisp.

## Architecture

The recommended first customer-hosted package is Docker Compose on a Linux VM:

- `web`: Next.js frontend.
- `api`: ASP.NET Core API.
- `solver`: Python OR-Tools scheduler.
- `postgres`: customer-owned PostgreSQL 16.
- `redis`: customer-owned Redis 7.
- `minio` or external S3-compatible storage.
- `seq`: local structured logs.
- Optional reverse proxy/WAF in front of `web` and `api`.

Minimum starting VM:

| Size | Use |
|---|---|
| 4 vCPU / 8 GB RAM | Small pilot without local AI |
| 8 vCPU / 16 GB RAM | Production baseline without local AI |
| GPU host or separate GPU server | Required for useful no-export local AI |

## Data Boundary

Customer-hosted means:

- PostgreSQL, Redis, uploads, logs, and backups are customer-controlled.
- AI requests go only to `AI_BASE_URL` when configured.
- Email/SMS/WhatsApp go only to configured providers.
- Analytics, chat widgets, and error tracking should be disabled unless the
  customer approves them.
- OfekLabs support should receive sanitized logs or customer-approved exports,
  not direct production database access by default.

## Install Steps

1. Prepare a Linux VM with Docker Engine, Docker Compose v2, Git, curl, and a
   TLS reverse proxy.
2. Clone the repository:

   ```bash
   sudo mkdir -p /opt/shifter
   sudo chown "$USER":"$USER" /opt/shifter
   git clone <repo-url> /opt/shifter
   cd /opt/shifter
   ```

3. Create the customer env file:

   ```bash
   cp infra/compose/.env.customer.example infra/compose/.env
   ```

4. Replace every `change_me_*` value and set the customer domains:

   ```env
   SHIFTER_DEPLOYMENT_MODE=customer-hosted
   APP_FRONTEND_BASE_URL=https://shifter.customer.example
   APP_API_BASE_URL=https://api-shifter.customer.example
   NEXT_PUBLIC_API_URL=https://api-shifter.customer.example
   NEXT_PUBLIC_LEGAL_EMAIL=it-support@customer.example
   FIELD_ENCRYPTION_KEY=<unique-customer-secret-at-least-32-chars>
   ```

5. Validate the env file:

   ```bash
   bash infra/scripts/validate-customer-env.sh
   ```

   On Windows/PowerShell:

   ```powershell
   .\infra\scripts\validate-customer-env.ps1
   ```

   Before a customer install or demo, run the package preflight from the repo
   root against the checked-in customer template:

   ```powershell
   .\infra\scripts\test-customer-hosted-package.ps1
   ```

   This runs the customer env validator harness, restore dry-run harness,
   backup harness, deploy happy-path harness, deploy rollback harness, Compose
   script syntax checks, `docker compose config`, and a temporary PostgreSQL
   organization import smoke against the customer env template.

   The same package preflight is also covered by the
   `Customer-Hosted Preflight` GitHub Actions workflow for relevant changes, so
   package drift should fail before merge.

   Before installing with real customer secrets, run the same preflight against
   the actual env file and require the env validator to pass:

   ```powershell
   .\infra\scripts\test-customer-hosted-package.ps1 `
     -EnvFile .\infra\compose\.env `
     -ValidateEnvFile
   ```

   If Docker is not available on the workstation running the preflight, add
   `-SkipDockerComposeConfig -SkipPostgresImportSmoke` and run those Docker-based
   checks on the target host.

   On the target host, the full verification wrapper runs the real env
   validation, package preflight, seed target check, and live smoke in order:

   ```powershell
   .\infra\scripts\verify-customer-hosted-install.ps1 `
     -EnvFile .\infra\compose\.env `
     -SeedDryRun `
     -ResolveOnly
   ```

   Remove `-SeedDryRun -ResolveOnly` after the stack is running and you are ready
   to load demo seed data and call the live web/API services. Add
   `-SkipBrowserTest` for API-only smoke verification.

6. Start the stack:

   ```bash
   cd infra/compose
   docker compose --project-name shifter up -d --build
   ```

7. Check health:

   ```bash
   curl -fsS http://127.0.0.1:5000/health
   curl -fsS http://127.0.0.1:5000/health/detailed
   curl -fsS http://127.0.0.1:3000
   ```

   The detailed report includes `ai`, `resend`, `push`, `solver`, `postgres`,
   and `redis`. The `ai` check is `skipped` when AI is disabled, and otherwise
   calls `{AI_BASE_URL}/models` without sending prompts, schedules, files, or
   customer data. The `push` check validates VAPID configuration locally and
   does not contact external push providers.

   Platform admins can also review the same provider status inside Shifter on
   the Platform page.

   Before a customer demo using the seeded self-service dataset, load the demo
   seed into the running Compose PostgreSQL service:

   ```bash
   ENV_FILE=/opt/shifter/infra/compose/.env \
     COMPOSE_PROJECT_NAME=shifter \
     /opt/shifter/infra/scripts/seed-compose.sh
   ```

   To validate the resolved Compose project, database, user, and seed file
   without writing demo data, add `DRY_RUN=1`.

   Then run the live smoke:

   ```powershell
   .\infra\scripts\smoke-self-service-client-ready.ps1 `
     -EnvFile .\infra\compose\.env
   ```

   This verifies the web/API endpoints, seeded users, the self-service demo
   cycle, available member slots, and the holiday/special-day picker label
   browser flow. For API/seed verification without the browser flow, add
   `-SkipBrowserTest`. The script checks already-running services; it does not
   start Docker Compose. It reads `APP_FRONTEND_BASE_URL`,
   `NEXT_PUBLIC_API_URL`/`APP_API_BASE_URL`, and optional `E2E_ADMIN_EMAIL`,
   `E2E_MEMBER_EMAIL`, and `E2E_DEMO_PASSWORD` from the env file. To verify the
   resolved values without calling live services, add `-ResolveOnly`.

8. Put HTTPS in front of the web/API ports using the customer's proxy or WAF.
   For Cloudflare, start from the
   [Cloudflare edge security baseline](CLOUDFLARE-EDGE-SECURITY.md).

## AI Choices

Shifter uses an OpenAI-compatible chat completions endpoint.

### Hosted AI

Use this for standard SaaS or customers who approve a cloud AI processor:

```env
AI_API_KEY=sk_...
AI_BASE_URL=https://api.openai.com/v1
AI_MODEL=gpt-4o
```

### Customer Cloud AI

Use this when the customer owns the AI account, for example Azure OpenAI through
an OpenAI-compatible gateway:

```env
AI_API_KEY=customer_managed_key
AI_BASE_URL=https://customer-ai-gateway.example.com/v1
AI_MODEL=gpt-4o
```

### No-Export Local AI

Use this when prompts, files, and schedule data must not leave the customer
network. The customer runs vLLM, Ollama, LM Studio, or another
OpenAI-compatible server:

```env
AI_API_KEY=
AI_BASE_URL=http://local-ai.customer.internal:8000/v1
AI_MODEL=customer-approved-model
AI_NO_EXPORT_REQUIRED=true
```

If no AI variables are set, Shifter starts with AI disabled. The scheduling
solver still works because it is deterministic OR-Tools logic, not hosted AI.
Manual self-service scheduling also works without hosted AI; see
[Manual self-service scheduling](MANUAL-SELF-SERVICE-SCHEDULING.md).

When `AI_NO_EXPORT_REQUIRED=true`, the customer env validators and the API
startup guard reject hosted/default AI and require `AI_BASE_URL` to use
localhost, a private IP, `.internal`, or `.local`.

## Email, Messaging, And Billing

For customer-hosted deployments:

- Prefer the customer's Resend, SMTP relay, or email gateway credentials.
- Leave `RESEND_API_KEY` empty if email must be disabled during a pilot.
- Leave Twilio empty unless WhatsApp delivery is approved.
- Do not use LemonSqueezy inside a private enterprise install unless the
  contract explicitly requires self-service billing.

## Manual Self-Service Defaults

Set these before switching customer groups to `SelfService` mode when the
organization needs policy defaults different from Shifter's built-ins:

```env
SELF_SERVICE_DEFAULT_MIN_SHIFTS_PER_CYCLE=1
SELF_SERVICE_DEFAULT_MAX_SHIFTS_PER_CYCLE=5
SELF_SERVICE_DEFAULT_CANCELLATION_CUTOFF_HOURS=36
SELF_SERVICE_DEFAULT_MAX_ABSENCES_PER_CYCLE=2
SELF_SERVICE_DEFAULT_MAX_LATE_CANCELLATIONS_PER_CYCLE=1
SELF_SERVICE_DEFAULT_ALLOW_SHIFT_SWAPS=false
```

The values are applied only when the group's self-service policy record is first
created. Existing group policies remain under admin control and are not
overwritten by later env changes.

Space owners can also configure the same template inside Shifter from
`Space Settings` -> `Self-Service`. A saved space template is used before the
install-level env defaults when a group is first switched to `SelfService`.
For multi-space organizations, platform admins can set an organization-level
self-service defaults template through
`/platform/organizations/{organizationId}/self-service-defaults`; spaces without
their own template inherit the organization template before falling back to the
install-level env defaults.

## Backups

The compose backup script creates PostgreSQL and upload-volume backups:

```bash
SHIFTER_DIR=/opt/shifter bash /opt/shifter/infra/scripts/backup-compose.sh
```

Restore requires an explicit confirmation flag and should be run during a
maintenance window. By default, the restore script creates a `pre_restore_*.dump`
of the current target database before replacing it. If `RESTORE_UPLOADS=1`, it
also creates a `pre_restore_uploads_*.tar.gz` archive before replacing the
uploads volume. PostgreSQL restore runs in a single transaction with
exit-on-error, and stopped app services are restarted if the script fails after
taking them down.

```bash
DRY_RUN=1 \
DB_BACKUP=/opt/shifter/backups/postgres_shifter_20260612_030000.dump \
SHIFTER_DIR=/opt/shifter \
bash /opt/shifter/infra/scripts/restore-compose.sh
```

```bash
CONFIRM=restore \
DB_BACKUP=/opt/shifter/backups/postgres_shifter_20260612_030000.dump \
SHIFTER_DIR=/opt/shifter \
bash /opt/shifter/infra/scripts/restore-compose.sh
```

To restore uploaded files from the matching `uploads_*.tar.gz` archive:

```bash
CONFIRM=restore \
RESTORE_UPLOADS=1 \
DB_BACKUP=/opt/shifter/backups/postgres_shifter_20260612_030000.dump \
UPLOADS_BACKUP=/opt/shifter/backups/uploads_shifter_20260612_030000.tar.gz \
SHIFTER_DIR=/opt/shifter \
bash /opt/shifter/infra/scripts/restore-compose.sh
```

Recommended production policy:

- Run backups at least daily.
- Copy backups to customer-owned off-host storage.
- Run `DRY_RUN=1` before any restore to validate the env file, backup paths,
  Compose project, and upload-volume plan without changing data.
- Keep the default pre-restore safety dumps enabled. Only use
  `SKIP_PRE_RESTORE_BACKUP=1` when the target database is already disposable or
  too damaged to dump, and the current uploads volume is safe to discard.
- Test restore before go-live and once per quarter.
- Keep database dumps and uploaded files under the same retention policy.
- If using an external S3-compatible bucket instead of local uploads, back up
  and restore that bucket through the customer's storage provider tooling.

## Upgrades

Use the deploy script for pull/build/health-check/rollback:

```bash
SHIFTER_DIR=/opt/shifter GIT_REF=main bash /opt/shifter/infra/scripts/deploy-compose.sh
```

For enterprise customers, agree on a maintenance window and take a backup before
each upgrade.

## Security Baseline

- Use HTTPS only for public access.
- Restrict Postgres, Redis, MinIO, and Seq to private network or localhost.
- Generate unique `JWT_SECRET`, database, Redis, MinIO, and Seq passwords.
- Generate a unique `FIELD_ENCRYPTION_KEY` and keep it stable for the lifetime
  of the customer database; changing it without a migration plan prevents
  protected contact fields from decrypting.
- Disable analytics/chat/error tracking unless approved.
- Leave `NEXT_PUBLIC_SENTRY_DSN`, `NEXT_PUBLIC_POSTHOG_KEY`, and
  `NEXT_PUBLIC_CRISP_WEBSITE_ID` empty unless the customer explicitly approves
  those processors. Empty Sentry DSN disables Sentry even in production builds,
  empty PostHog key disables PostHog tracking calls, and empty Crisp website ID
  prevents the chat widget from loading.
- Configure provider credentials as complete groups: Resend requires API key
  plus sender email/name, Twilio requires SID/token/from number, Web Push
  requires all VAPID keys plus matching public frontend key, Pushover requires
  both alert keys, and LemonSqueezy requires all billing/webhook identifiers.
  The customer env validator fails partial groups.
- Put WAF/rate limits in front of auth, billing, import, solver, uploads, and
  admin endpoints. See
  [Cloudflare edge security baseline](CLOUDFLARE-EDGE-SECURITY.md) for the
  first rule set.
- Keep OS and Docker patched.
- Store env files in customer secrets management where possible.

## What Is Not Yet Packaged

These are good next iterations, but not required for the first sellable
customer-hosted package:

- Helm chart for Kubernetes customers.
- Offline image bundle for air-gapped sites.
- License/entitlement enforcement for on-prem contracts.
