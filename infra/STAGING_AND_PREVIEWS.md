# Staging and Preview Deployments on Hetzner

This project is currently shaped for a single VPS deployment with Docker Compose and Caddy. For safer releases, use three gates:

1. Feature branches for work.
2. `develop` deployed to a persistent staging URL.
3. `main` deployed to production only after staging is verified.

## Recommended Setup

Use the same Hetzner VPS if it has enough CPU/RAM, but run staging as a separate Compose project with separate data volumes and ports. For higher isolation, use a second small Hetzner VPS for staging.

Recommended path: start with a separate staging VPS if the budget is acceptable. It keeps production safer, makes staging deploy failures less stressful, and lets you test VPS-level changes such as Caddy, Docker, backups, and firewall rules before touching production.

Minimum recommended topology:

| Environment | Git ref | URL | Data |
| --- | --- | --- | --- |
| Production | `main` | `https://app.example.com` | production DB/storage/secrets |
| Staging | `develop` | `https://staging.example.com` | separate DB/storage/secrets |
| PR preview | PR branch | optional `https://pr-123.example.com` | ephemeral or shared staging-like data |

Do not point staging at the production database, Redis, S3 bucket, LemonSqueezy live keys, Resend production sender, or Twilio production sender.

## Persistent Staging on the VPS

If staging runs on its own VPS, keep the default internal ports from `.env.staging.example` or switch them back to production-like ports. Port separation matters most when production and staging share one server. The important separation on a different VPS is secrets, data, DNS, and branch.

1. Create DNS records:
   - `staging.example.com` -> VPS IP
   - `staging-api.example.com` -> VPS IP, unless API is served under `/api` on the staging frontend domain

2. Clone a second working copy:

```bash
cd /opt
git clone https://github.com/OfekItzhaki/Shifter.git shifter-staging
cd /opt/shifter-staging
git checkout develop
```

3. Create a staging env file:

```bash
cd /opt/shifter-staging/infra/compose
cp .env.staging.example .env
```

Edit `.env` and replace:

- every password and JWT secret
- `APP_FRONTEND_BASE_URL`
- `APP_API_BASE_URL`
- `NEXT_PUBLIC_API_URL`
- staging-only email, billing, push, storage, analytics, and monitoring values

4. Start staging:

```bash
cd /opt/shifter-staging/infra/compose
docker compose --project-name shifter-staging up -d --build
```

5. Add Caddy routes. Example:

```caddy
staging.example.com {
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

staging-api.example.com {
    basicauth {
        admin <bcrypt-password-hash>
    }

    handle /health {
        reverse_proxy localhost:15000
    }

    handle /* {
        reverse_proxy localhost:15000
    }
}
```

Generate a Caddy password hash:

```bash
docker run --rm caddy:2-alpine caddy hash-password --plaintext 'choose-a-strong-password'
```

6. Deploy staging from `develop`:

```bash
GIT_REF=develop \
SHIFTER_DIR=/opt/shifter-staging \
COMPOSE_PROJECT_NAME=shifter-staging \
/opt/shifter-staging/infra/scripts/deploy-compose.sh
```

See `infra/AVAILABILITY.md` for the backup, health verification, and rollback flow used by this script.

## GitHub Staging Deploy Workflow

The `Deploy Staging` workflow deploys `develop` with the same
`infra/scripts/deploy-compose.sh` path.

It is safe by default:

- manual dispatch must run from the `develop` ref
- pushes to `develop` deploy only when repository variable
  `ENABLE_STAGING_DEPLOY=true`
- the workflow uses the GitHub `staging` environment, so add environment
  protection/approval rules if the staging host should not update unattended
- the hosted smoke check runs only when both `STAGING_WEB_BASE_URL` and
  `STAGING_API_BASE_URL` repository variables are set

Required staging secrets:

- `STAGING_HOST`
- `STAGING_USER`
- `STAGING_SSH_KEY`
- optional `STAGING_PORT`

Fallback secrets, if you prefer shared Hetzner names:

- `HETZNER_HOST`
- `HETZNER_USER`
- `HETZNER_SSH_KEY`

Recommended staging variables:

- `ENABLE_STAGING_DEPLOY=false` until the staging host is ready
- `STAGING_PATH=/opt/shifter-staging`
- `STAGING_COMPOSE_PROJECT_NAME=shifter-staging`
- `STAGING_WEB_BASE_URL=https://staging.example.com`
- `STAGING_API_BASE_URL=https://staging-api.example.com`

## Production Deploy Rule

Production should deploy only from `main`.

Suggested rule:

1. Work lands on `develop`.
2. `develop` deploys to staging.
3. You test `https://staging.example.com`.
4. Open a PR from `develop` to `main`.
5. Merge only after checks pass and staging is approved.
6. Production deploy pulls `main`.

Protect `main` in GitHub:

- require pull requests
- require status checks
- block force pushes
- optionally require manual approval for the production deploy environment

## PR Preview Options

Persistent staging is the first priority. PR previews are useful later, but they are more complex on a single VPS.

### Option A: Preview Frontend Only

Use Vercel or Cloudflare Pages for `apps/web` preview builds while keeping the API pointed at staging. This is the simplest way to get a clickable URL for UI review.

Tradeoff: it does not test API/backend changes in isolation.

### Option B: One Ephemeral Compose Stack Per PR

Use GitHub Actions to SSH into the VPS and run one Compose project per PR:

```bash
COMPOSE_PROJECT_NAME=shifter-pr-123
WEB_PORT=13123
API_PORT=15123
POSTGRES_PORT=16123
REDIS_PORT=17123
MINIO_PORT=19123
MINIO_CONSOLE_PORT=19124
```

Then add a Caddy route for `pr-123.example.com` to `localhost:13123`.

Tradeoffs:

- needs port allocation logic
- needs cleanup when PRs close
- can consume a lot of RAM/CPU because each preview runs Postgres, Redis, API, solver, and web
- should be protected with basic auth

### Option C: Second VPS for Previews

Best long-term option if PR previews become important. Keep production stable on the main VPS and run staging/previews on a cheaper separate VPS.

## What to Automate Next

Add GitHub Actions:

- PR: run web typecheck, API tests, solver tests where available
- push to `develop`: SSH deploy staging
- push to `main`: SSH deploy production, protected by a GitHub Environment approval

Future front-door hardening:

- Add Cloudflare in front of staging/production for DNS, CDN, WAF, DDoS protection, and rate limiting.
- See `infra/CLOUDFLARE_FRONT_DOOR.md`.

Required GitHub secrets:

- `HETZNER_HOST`
- `HETZNER_USER`
- `HETZNER_SSH_KEY`
- `STAGING_PATH=/opt/shifter-staging`
- `PRODUCTION_PATH=/opt/shifter`

Keep environment secrets on the server in `.env`; do not store real `.env` files in Git.
