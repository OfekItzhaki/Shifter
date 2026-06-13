# Before Real Users Checklist

This is the minimum recommended path before onboarding real restaurant/business users into the hosted Shifter service.

## Current Deployment Branches

- Production deploys from `main`.
- Staging deploys from `develop`.
- Do not merge `develop` to `main` until staging has been deployed and manually tested.

The production workflow is `.github/workflows/deploy-vps.yml`. It runs on pushes to `main`, blocks non-`main` refs, and pulls `main` on the VPS.

The staging workflow is `.github/workflows/deploy-staging.yml`. It deploys `develop` to a staging host/path once staging variables and secrets are configured.

## Do Now

1. Enable Hetzner VPS backups for the current production VPS.
   - This protects the whole server disk.
   - It is not a replacement for database/uploads backups, but it is an easy safety layer.

2. Keep using the current production site from `main`.
   - Do not deploy `develop` to production yet.
   - Do not enable `STORAGE_LOCAL_PUBLIC_BASE_URL` or object-storage uploads yet.

3. Set up staging before the next production release.
   - Use `develop`.
   - Use separate database, Redis, uploads volume, ports, secrets, and domains.
   - Do not point staging at the production database or production uploads.

## Staging Setup

Recommended hostnames:

```text
staging.shifter.ofeklabs.com
staging-api.shifter.ofeklabs.com
```

Recommended staging path:

```text
/opt/shifter-staging
```

Recommended staging compose identity:

```env
COMPOSE_PROJECT_NAME=shifter-staging
POSTGRES_PORT=15432
REDIS_PORT=16379
API_PORT=15000
WEB_PORT=13000
MINIO_PORT=19000
MINIO_CONSOLE_PORT=19001
APP_FRONTEND_BASE_URL=https://staging.shifter.ofeklabs.com
APP_API_BASE_URL=https://staging-api.shifter.ofeklabs.com
NEXT_PUBLIC_API_URL=https://staging-api.shifter.ofeklabs.com
NEXT_PUBLIC_APP_VERSION=staging
AUTO_SCHEDULER_ENABLED=false
```

Use `infra/compose/.env.staging.example` as the starting point.

## Staging Verification

After staging deploys from `develop`, manually test:

- Landing page loads in all supported languages.
- Register creates only the account, then login sends the user to onboarding.
- Onboarding creates a real space/workspace.
- Login, logout, refresh session, password reset/change.
- Profile image upload and preview.
- PWA install/manifest/service worker.
- Create space, group, roles, people, tasks.
- Automatic scheduler path.
- Manual self-service mode:
  - enable self-service scheduling for a group
  - create shift templates
  - generate/open a cycle
  - member claims/request flow
  - waitlist/absence/change/swap flows as applicable
  - admin closeout/export flow
- Billing gates do not block normal trial usage unexpectedly.
- Hebrew/English/Russian UI does not show raw translation keys.

Run hosted smoke if staging URLs are configured:

```powershell
.\infra\scripts\smoke-hosted-vps.ps1 `
  -WebBaseUrl https://staging.shifter.ofeklabs.com `
  -ApiBaseUrl https://staging-api.shifter.ofeklabs.com
```

## Production Backups

Already verified manually on the VPS:

```text
postgres_compose_<timestamp>.dump
uploads_compose_<timestamp>.tar.gz
```

These files mean:

- PostgreSQL dump: app database snapshot.
- Uploads archive: profile images and local uploaded files from the compose uploads volume.

Before real users, finish:

1. Daily cron for `backup-compose.sh`.
2. Offsite copy to private storage.
3. One restore rehearsal into staging or another non-production database.

Cron only runs the backup command on a schedule. It does not prove the dump restores correctly. Restore rehearsal is the proof.

## Offsite Backup Recommendation

Use one private backup destination before real users:

- Hetzner Object Storage private bucket, or
- Hetzner Storage Box, or
- Cloudflare R2 private bucket.

Recommended simple first choice: Hetzner Object Storage, because the VPS is already on Hetzner.

Keep backups separate from app uploads:

```text
shifter-prod-backups    private, backup dumps/archives only
shifter-prod-uploads    future app uploads/profile images/files
```

Do not make backup buckets public.

## Upload Storage Recommendation

For launch:

- Keep local disk uploads.
- Keep `STORAGE_LOCAL_PUBLIC_BASE_URL` empty.
- Treat profile images as semi-public random URLs.
- Do not use public uploads for sensitive documents or customer-confidential exports.

Later:

- Move app uploads to S3-compatible object storage.
- Keep a stable public host such as `https://uploads.shifter.ofeklabs.com`.
- Add private/signed download paths for sensitive files.

## Merge To Main Gate

Merge `develop` to `main` only after:

- Staging deploy from `develop` succeeds.
- Manual staging checklist passes.
- Backup cron exists for production.
- At least one offsite backup copy exists.
- Restore rehearsal is completed or explicitly accepted as a launch risk.
- Known blockers are documented.

After merge, production deploy will use `main`.
