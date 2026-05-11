# Step 162 — VAPID Environment Variables

## Phase

Push Notifications — Frontend Environment Configuration

## Purpose

Add VAPID (Voluntary Application Server Identification) environment variables to the project's `.env.example` so developers know which variables to configure for web push notifications. The frontend needs the public key to create PushManager subscriptions, and the backend needs the full key pair to sign and encrypt push messages.

## What was built

| File | Change |
|------|--------|
| `infra/compose/.env.example` | Added `NEXT_PUBLIC_VAPID_PUBLIC_KEY` in the Frontend section with a placeholder value |
| `infra/compose/.env.example` | Added new "Web Push (VAPID)" section with `VAPID_PUBLIC_KEY`, `VAPID_PRIVATE_KEY`, and `VAPID_SUBJECT` for the backend |

## Key decisions

- **No `next.config.mjs` change needed**: Next.js automatically exposes any env var prefixed with `NEXT_PUBLIC_` to the client bundle. No explicit configuration required.
- **Placeholder value for frontend key**: Uses `your-vapid-public-key-here` to make it obvious the value must be replaced.
- **Backend keys left empty**: Following the same pattern as SendGrid/Twilio — empty values disable the feature gracefully.
- **Key generation hint**: Added a comment with `npx web-push generate-vapid-keys` so developers can easily generate a key pair.
- **Single `.env.example` location**: The project uses `infra/compose/.env.example` as the canonical env template (no per-app `.env.example` files).

## How it connects

- The `NEXT_PUBLIC_VAPID_PUBLIC_KEY` is consumed by the `usePushSubscription` hook (task 7.1) when calling `pushManager.subscribe({ applicationServerKey })`.
- The backend `VAPID_PUBLIC_KEY`, `VAPID_PRIVATE_KEY`, and `VAPID_SUBJECT` are consumed by the `VapidSettings` options class (task 1.3) and used by `PushNotificationSender` (task 3.2) to sign push requests.

## How to run / verify

1. Open `infra/compose/.env.example` and confirm the three new VAPID variables are present.
2. Copy `.env.example` to `.env` and generate real keys:
   ```bash
   npx web-push generate-vapid-keys
   ```
3. Paste the generated public key into both `NEXT_PUBLIC_VAPID_PUBLIC_KEY` and `VAPID_PUBLIC_KEY`, and the private key into `VAPID_PRIVATE_KEY`.

## What comes next

- Task 7.1 (usePushSubscription hook) reads `NEXT_PUBLIC_VAPID_PUBLIC_KEY` at runtime.
- Task 3.2 (PushNotificationSender) reads the backend VAPID keys via `VapidSettings`.

## Git commit

```bash
git add -A && git commit -m "feat(push): add VAPID environment variables to .env.example"
```
