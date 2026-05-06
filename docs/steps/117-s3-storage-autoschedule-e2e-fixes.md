# Step 117 — S3 Storage, Auto-Scheduler Config, E2E Fixes

## Phase
Phase 5 — Production Readiness

## Purpose
1. **S3-compatible file storage** — implemented `S3FileStorage` so the app can use AWS S3, MinIO, Cloudflare R2, or any S3-compatible service in production. Local disk remains the default when no S3 config is provided.
2. **Auto-scheduler enabled in dev** — added `AutoScheduler:Enabled=true` to `appsettings.Development.json` so it runs during local development.
3. **E2E test fixes** — fixed the `loginAsAdmin` helper to handle the `/spaces` redirect, added `data-testid="notification-bell"` for reliable test selection.
4. **Docker-compose wiring** — added S3, auto-scheduler, and app URL env vars to `docker-compose.yml` and `.env.example`.

## What was built

### Backend
- **`apps/api/Jobuler.Infrastructure/Storage/S3FileStorage.cs`** — new S3-compatible file storage implementation. Supports AWS S3, MinIO (with `ForcePathStyle=true`), Cloudflare R2, and any S3-compatible endpoint. Auto-selected when `Storage:S3:BucketName` is configured.
- **`apps/api/Jobuler.Infrastructure/Jobuler.Infrastructure.csproj`** — added `AWSSDK.S3` package reference.
- **`apps/api/Jobuler.Api/Program.cs`** — updated DI registration to auto-select S3 vs local disk based on config.
- **`apps/api/Jobuler.Api/appsettings.Development.json`** — enabled auto-scheduler (`AutoScheduler:Enabled: true`).

### Infrastructure
- **`infra/compose/.env.example`** — added S3 config vars, auto-scheduler toggle, app URL vars.
- **`infra/compose/docker-compose.yml`** — wired all new env vars to the API service.

### Frontend
- **`apps/web/components/shell/NotificationBell.tsx`** — added `data-testid="notification-bell"` for reliable E2E test selection.
- **`apps/web/e2e/helpers/auth.ts`** — fixed `loginAsAdmin` to handle the `/spaces` redirect after login.
- **`apps/web/e2e/admin-nav.spec.ts`** — updated notification bell test to use `data-testid` selector.

## Key decisions
- S3 is opt-in: if `Storage:S3:BucketName` is not set, the app falls back to local disk. No breaking change.
- `ForcePathStyle` is automatically set to `true` when a custom `ServiceUrl` is provided (required for MinIO).
- Files are stored with `public-read` ACL. For private buckets, swap to presigned URLs.
- Auto-scheduler is enabled in dev so you can test the full scheduling loop locally.

## How to configure S3 (MinIO example)
```env
STORAGE_S3_BUCKET_NAME=shifter-uploads
STORAGE_S3_ACCESS_KEY=minioadmin
STORAGE_S3_SECRET_KEY=changeme_minio
STORAGE_S3_SERVICE_URL=http://minio:9000
STORAGE_S3_PUBLIC_BASE_URL=http://localhost:9000/shifter-uploads
STORAGE_S3_FORCE_PATH_STYLE=true
```

## How to configure S3 (AWS example)
```env
STORAGE_S3_BUCKET_NAME=my-shifter-bucket
STORAGE_S3_REGION=us-east-1
STORAGE_S3_ACCESS_KEY=AKIAIOSFODNN7EXAMPLE
STORAGE_S3_SECRET_KEY=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY
STORAGE_S3_PUBLIC_BASE_URL=https://my-shifter-bucket.s3.amazonaws.com
```

## E2E test results (auth.spec.ts)
- ✅ login page renders
- ✅ invalid credentials shows error
- ✅ valid credentials redirects away from login

## Git commit
```bash
git add -A && git commit -m "feat(storage): S3-compatible file storage, auto-scheduler in dev, E2E fixes"
```
