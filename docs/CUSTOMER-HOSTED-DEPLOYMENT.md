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
   ```

5. Validate the env file:

   ```bash
   bash infra/scripts/validate-customer-env.sh
   ```

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

   The detailed report includes `ai`, `resend`, `solver`, `postgres`, and
   `redis`. The `ai` check is `skipped` when AI is disabled, and otherwise calls
   `{AI_BASE_URL}/models` without sending prompts, schedules, files, or customer
   data.

8. Put HTTPS in front of the web/API ports using the customer's proxy or WAF.

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
```

If no AI variables are set, Shifter starts with AI disabled. The scheduling
solver still works because it is deterministic OR-Tools logic, not hosted AI.
Manual self-service scheduling also works without hosted AI; see
[Manual self-service scheduling](MANUAL-SELF-SERVICE-SCHEDULING.md).

## Email, Messaging, And Billing

For customer-hosted deployments:

- Prefer the customer's Resend, SMTP relay, or email gateway credentials.
- Leave `RESEND_API_KEY` empty if email must be disabled during a pilot.
- Leave Twilio empty unless WhatsApp delivery is approved.
- Do not use LemonSqueezy inside a private enterprise install unless the
  contract explicitly requires self-service billing.

## Backups

The compose backup script creates PostgreSQL and upload-volume backups:

```bash
SHIFTER_DIR=/opt/shifter bash /opt/shifter/infra/scripts/backup-compose.sh
```

Recommended production policy:

- Run backups at least daily.
- Copy backups to customer-owned off-host storage.
- Test restore before go-live and once per quarter.
- Keep database dumps and uploaded files under the same retention policy.

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
- Disable analytics/chat/error tracking unless approved.
- Put WAF/rate limits in front of auth, billing, import, solver, and admin
  endpoints.
- Keep OS and Docker patched.
- Store env files in customer secrets management where possible.

## What Is Not Yet Packaged

These are good next iterations, but not required for the first sellable
customer-hosted package:

- Helm chart for Kubernetes customers.
- Admin UI for AI/email/provider health checks.
- One-command restore script.
- Offline image bundle for air-gapped sites.
- License/entitlement enforcement for on-prem contracts.
