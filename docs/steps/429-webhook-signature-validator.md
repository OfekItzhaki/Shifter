# 429 — Webhook Signature Validator

## Phase

LemonSqueezy Billing Integration — Infrastructure Layer

## Purpose

Provides a secure mechanism to verify the authenticity of incoming LemonSqueezy webhook requests. Uses HMAC-SHA256 with the configured webhook secret to compute a hash of the raw payload and compares it against the signature header using a timing-safe comparison to prevent timing attacks.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Application/Billing/IWebhookSignatureValidator.cs` | Interface defining the `Verify(string payload, string signature)` contract in the Application layer |
| `Jobuler.Infrastructure/Billing/WebhookSignatureValidator.cs` | Implementation using HMAC-SHA256 with `CryptographicOperations.FixedTimeEquals` for timing-safe comparison |

## Key decisions

- **Timing-safe comparison**: Uses `CryptographicOperations.FixedTimeEquals` to prevent timing side-channel attacks that could leak information about the expected signature.
- **Hex encoding**: LemonSqueezy sends signatures as lowercase hex strings, so the computed hash is converted to lowercase hex before comparison.
- **IOptions<LemonSqueezySettings>**: The validator receives settings via `IOptions<T>` pattern, consistent with .NET configuration best practices. This allows it to be registered as a singleton since the secret doesn't change at runtime.
- **Early return on empty inputs**: Returns `false` immediately for null/empty payload or signature to avoid unnecessary computation.
- **Static HMACSHA256.HashData**: Uses the static one-shot API (available in .NET 8) which is more efficient than creating and disposing an HMAC instance per call.

## How it connects

- **Depends on**: `LemonSqueezySettings.WebhookSecret` (task 2.1)
- **Consumed by**: `LemonSqueezyWebhookController` (task 7.1) to verify incoming webhook requests
- **Registered in**: DI container as singleton (task 2.5)

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build succeeds with no new warnings. Full integration verification happens in task 7.1 when the webhook controller uses this validator.

## What comes next

- Task 2.5: DI registration (`IWebhookSignatureValidator` → `WebhookSignatureValidator` as singleton)
- Task 7.1: `LemonSqueezyWebhookController` uses this to verify webhook signatures

## Git commit

```bash
git add -A && git commit -m "feat(billing): add IWebhookSignatureValidator interface and HMAC-SHA256 implementation"
```
