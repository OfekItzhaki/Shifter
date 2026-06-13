# Production Backups And Upload Storage

This runbook is for the OfekLabs-hosted Shifter VPS deployment.

## Why an Uploads URL helps

`https://uploads.shifter.ofeklabs.com` is not required for launch, but it gives uploaded files a stable public home that is separate from the API.

Benefits:

- The API can move from VPS local disk to S3-compatible storage later without changing image URLs stored in the database.
- Static upload traffic can be served by Nginx, a CDN, or object storage instead of the API process.
- Cache rules, size limits, access logs, and security headers can be managed separately from app routes.
- If the API host changes, profile image URLs do not need to change.

For the first hosted pilot, local disk uploads are acceptable only if backups are working and copied off the VPS.

## Current risk

The dangerous state is:

- PostgreSQL data only exists on the VPS.
- Uploaded profile images only exist on the VPS.
- No verified backup exists outside the VPS.

A VPS disk problem could then lose customer data and profile images. Database loss is the most critical risk; upload loss is still a trust problem.

## One-time manual backup

SSH into the VPS and run:

```bash
cd /opt/shifter
COMPOSE_PROJECT_NAME=compose ./infra/scripts/backup-compose.sh
```

If the production compose project name is different, set it explicitly:

```bash
cd /opt/shifter
COMPOSE_PROJECT_NAME=shifter ./infra/scripts/backup-compose.sh
```

Verify the files:

```bash
ls -lh /opt/shifter/backups
```

You should see both:

- `postgres_<project>_<timestamp>.dump`
- `uploads_<project>_<timestamp>.tar.gz`

## Verify restore inputs

The database dump should be non-empty:

```bash
du -h /opt/shifter/backups/postgres_*.dump
```

The uploads archive should list files or at least be a valid tar archive:

```bash
tar -tzf "$(ls -t /opt/shifter/backups/uploads_*.tar.gz | head -n 1)" | head
```

If the uploads archive is missing, confirm the compose project name:

```bash
docker volume ls | grep uploads
```

The expected volume name is usually `<compose-project>_uploads_data`.

## Daily cron

Open root's crontab:

```bash
sudo crontab -e
```

Add:

```cron
15 3 * * * cd /opt/shifter && COMPOSE_PROJECT_NAME=compose /opt/shifter/infra/scripts/backup-compose.sh >> /var/log/shifter-backup.log 2>&1
```

Then verify cron is installed:

```bash
sudo crontab -l
```

After the next run, check:

```bash
tail -n 100 /var/log/shifter-backup.log
ls -lh /opt/shifter/backups
```

## Copy backups off the VPS

Install rclone:

```bash
curl https://rclone.org/install.sh | sudo bash
```

Configure an S3-compatible remote:

```bash
rclone config
```

Suggested remote names:

- `r2` for Cloudflare R2.
- `hetzner` for Hetzner Object Storage.

Create a bucket such as:

```text
shifter-prod-backups
```

Test copying one backup:

```bash
rclone copy /opt/shifter/backups r2:shifter-prod-backups/prod --include "*.dump" --include "*.tar.gz" --dry-run
rclone copy /opt/shifter/backups r2:shifter-prod-backups/prod --include "*.dump" --include "*.tar.gz"
rclone ls r2:shifter-prod-backups/prod | tail
```

For Hetzner, replace `r2:` with the remote name you configured.

## Daily offsite sync cron

After the local backup cron line, add:

```cron
45 3 * * * rclone copy /opt/shifter/backups r2:shifter-prod-backups/prod --include "*.dump" --include "*.tar.gz" >> /var/log/shifter-backup-offsite.log 2>&1
```

Verify:

```bash
tail -n 100 /var/log/shifter-backup-offsite.log
rclone ls r2:shifter-prod-backups/prod | tail
```

## Production S3-compatible uploads

Recommended path:

1. Keep local disk uploads for the first pilot only after backups are verified.
2. Add an S3-compatible bucket for uploads.
3. Configure the API with the existing `Storage__S3__*` settings.
4. Point `Storage__S3__PublicBaseUrl` at a clean URL such as `https://uploads.shifter.ofeklabs.com`.
5. Add DNS and reverse proxy/CDN rules for the uploads host.
6. Migrate existing local uploads to the bucket.

Example environment values:

```env
STORAGE_S3_BUCKET_NAME=shifter-prod-uploads
STORAGE_S3_REGION=auto
STORAGE_S3_ACCESS_KEY=<access-key>
STORAGE_S3_SECRET_KEY=<secret-key>
STORAGE_S3_SERVICE_URL=https://<account-or-provider-endpoint>
STORAGE_S3_PUBLIC_BASE_URL=https://uploads.shifter.ofeklabs.com
STORAGE_S3_FORCE_PATH_STYLE=true
```

Do not commit these values. Store them only in the VPS `.env` or a secret manager.

## Minimum launch requirement

Before onboarding real users, complete at least:

- One successful manual backup.
- One successful cron backup.
- One successful offsite copy.
- One restore rehearsal on a non-production database.
- Confirmation that uploaded profile images are included in the uploads archive or stored in S3-compatible object storage.
