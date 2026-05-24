# Step 500 — Webhook Signature Rejection Property Test

## Phase

Phase 10 — API Layer Property Tests (Space Billing)

## Purpose

Implements Property 17 from the space-billing design document: verifying that any webhook request with an invalid HMAC signature is rejected with 401 Unauthorized and causes no state modification (no WebhookEventLog created, no SpaceSubscription modified, no commands dispatched).

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Tests/Billing/WebhookSignatureRejectionPropertyTests.cs` | FsCheck property-based tests (4 properties, 100 iterations each) validating webhook signature rejection at the controller level |

## Key decisions

- **Controller-level testing**: Tests instantiate `LemonSqueezyWebhookController` directly with a real `WebhookSignatureValidator` and mock `IMediator`, simulating the full signature verification path without needing an integration test host.
- **Real validator, fake HTTP context**: Uses `DefaultHttpContext` with a `MemoryStream` body and `X-Signature` header to simulate incoming webhook requests.
- **Four complementary properties**: (1) random invalid signatures return 401, (2) signatures computed with wrong secret return 401, (3) no WebhookEventLog is created, (4) pre-existing SpaceSubscription is not modified.
- **Generator design**: Separate generators for payloads, secrets, and invalid signatures ensure broad coverage of the input space.

## How it connects

- Validates Requirement 5.4 from the space-billing spec
- Tests the same `WebhookSignatureValidator` and `LemonSqueezyWebhookController` used in production
- Complements the existing `WebhookSignatureAndIdempotencyPropertyTests.cs` which covers the old lemonsqueezy-billing spec properties

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~WebhookSignatureRejectionPropertyTests"
```

All 4 tests should pass (100 iterations each).

## What comes next

- Task 13.3: Days remaining and color computation property test (frontend)
- Task 14.3: SpaceBillingCard unit tests

## Git commit

```bash
git add -A && git commit -m "feat(billing): add webhook signature rejection property test (Property 17)"
```
