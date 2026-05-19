# 437 — LemonSqueezy Webhook Controller

## Phase
LemonSqueezy Billing Integration — API Layer

## Purpose
Provides the public HTTP endpoint that receives webhook events from LemonSqueezy. Since LemonSqueezy cannot provide bearer tokens, this endpoint is `[AllowAnonymous]` and relies on HMAC signature verification for security.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Api/Controllers/LemonSqueezyWebhookController.cs` | New controller with `POST /webhooks/lemonsqueezy` endpoint |

## Key decisions

- **AllowAnonymous**: Required because LemonSqueezy cannot send bearer tokens. Security is enforced via HMAC-SHA256 signature in the `X-Signature` header.
- **Raw body reading**: Uses `StreamReader` to read the raw request body before parsing, since the signature must be verified against the exact bytes sent.
- **Fail-fast on malformed payloads**: Returns 400 immediately if JSON is invalid or required fields (`meta.event_name`, event ID) are missing.
- **Event ID extraction**: Tries `meta.webhook_id` first (LemonSqueezy v1 format), falls back to `data.id`.
- **Metadata extraction**: Reads `meta.custom_data` object into a flat `Dictionary<string, string>` for downstream handlers.
- **Always 200 for dispatched events**: Once the payload is valid and dispatched to MediatR, always returns 200 to LemonSqueezy (idempotency and error handling are in the application layer).

## How it connects

- Depends on `IWebhookSignatureValidator` (Infrastructure) for signature verification
- Dispatches `HandleWebhookCommand` (Application) via MediatR for event processing
- Works alongside `BillingController` which handles checkout and test-charge endpoints

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The endpoint is accessible at `POST /webhooks/lemonsqueezy` without authentication.

## What comes next

- Task 7.2: Add checkout endpoint to BillingController
- Task 7.3: Add test-charge endpoint to BillingController
- Task 7.4: Property tests for webhook signature verification and idempotency

## Git commit

```bash
git add -A && git commit -m "feat(billing): add LemonSqueezy webhook controller endpoint"
```
