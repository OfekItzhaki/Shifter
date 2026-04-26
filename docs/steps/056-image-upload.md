# Step 056 — Image Upload

## Phase
Phase 8 — Production Hardening

## Purpose
Replace the "paste a URL" pattern for profile photos, group images, and space logos with actual file upload. Built with a storage abstraction so local disk is used in dev and S3 (or any cloud provider) can be swapped in for production via DI config.

## What was built

### `apps/api/Jobuler.Application/Common/IFileStorage.cs`
Storage abstraction with two methods: `SaveAsync` (returns public URL) and `DeleteAsync`. Lives in Application so handlers can depend on it without touching Infrastructure.

### `apps/api/Jobuler.Application/Common/ImageValidator.cs`
Security utility that validates uploaded images three ways:
- Content-Type header check (JPEG, PNG, WebP, GIF only)
- File size check (max 10 MB)
- Magic byte validation — reads the first 12 bytes of the stream and verifies they match a known image signature. Prevents disguised executables (e.g. a `.exe` renamed to `.jpg`).

### `apps/api/Jobuler.Infrastructure/Storage/LocalDiskFileStorage.cs`
Saves files to `wwwroot/uploads/` with a random UUID filename (prevents path traversal and collisions). Returns a public URL using `App:ApiBaseUrl` from config. Swap for `S3FileStorage` in production by changing the DI registration in `Program.cs`.

### `apps/api/Jobuler.Api/Controllers/UploadsController.cs`
`POST /uploads/image` — accepts `multipart/form-data`, runs all three validations, saves the file, returns `{ url }`. Requires auth. Request size limit set to 10 MB + multipart overhead.

### `apps/api/Jobuler.Api/Program.cs`
- Registered `IFileStorage → LocalDiskFileStorage` in DI
- Added `using Jobuler.Infrastructure.Storage`
- Added `app.UseStaticFiles()` before `UseCors()` so `/uploads/*` is served

### `apps/api/Jobuler.Api/appsettings.json` + `appsettings.Development.json`
Added `App:ApiBaseUrl` config key:
- Dev: `http://localhost:5000`
- Prod: `https://api.jobuler.app`

### `apps/web/lib/api/uploads.ts`
`uploadImage(file: File): Promise<string>` — posts multipart form to `/uploads/image`, returns the URL.

### `apps/web/components/ImageUpload.tsx`
Reusable React component. Features:
- Click to open file picker (JPEG/PNG/WebP/GIF)
- Shows current image as preview with hover overlay
- Client-side 10 MB size check before upload
- Uploading spinner + error display
- `shape` prop: "circle" (avatars) or "square" (logos/banners)
- `size` prop for preview dimensions

### `apps/web/app/profile/page.tsx`
Replaced the "profile image URL" text input with `<ImageUpload>` component.

## Key decisions
- Storage abstraction in Application layer — handlers can call `IFileStorage` without knowing about disk or S3
- Random UUID filenames — no path traversal, no collisions, no info leakage
- Magic byte validation — content-type headers are trivially spoofed; actual file bytes are the ground truth
- `UseStaticFiles()` serves uploads in dev — no extra server needed; in prod, a CDN or S3 presigned URL replaces this
- Single upload endpoint for all image types — the caller decides what to do with the returned URL

## How it connects
- Any entity with an image field (Person, Group, Space) calls `POST /uploads/image` first, gets a URL, then saves that URL via the entity's own update endpoint
- `ImageUpload` component is drop-in: pass `value` (current URL) and `onChange` (receives new URL after upload)

## How to run / verify
1. Restart the API
2. Open Swagger at `http://localhost:5000/swagger`
3. Authenticate, then `POST /uploads/image` with a JPEG file
4. Verify response: `{ "url": "http://localhost:5000/uploads/<uuid>.jpg" }`
5. Open the URL in a browser — image should load
6. Try uploading a non-image file — should get 400 with a clear error message
7. On the frontend, go to `/profile`, click Edit, click the avatar circle to upload a photo

## What comes next
- Add `S3FileStorage` implementation for production (swap DI registration)
- Wire `ImageUpload` into group detail page and space settings when those edit forms are built
- Optionally add image resizing/compression before storage

## Git commit
```bash
git add -A && git commit -m "feat(uploads): image upload endpoint with magic-byte validation, local disk storage, ImageUpload component"
```
