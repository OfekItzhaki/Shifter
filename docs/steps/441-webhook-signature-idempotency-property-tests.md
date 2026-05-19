# Step 441 — Webhook Signature & Idempotency Property Tests

## Phase

Phase: LemonSqueezy Billing Integration — Property-Based Testing

## Purpose

Validates the correctness of webhook signature verification and idempotent event processing through property-based tests using FsCheck. These tests ensure that:
- Valid HMAC-SHA256 signatures are accepted and invalid/tampered ones are rejected
- Malformed payloads cannot pass validation
- Unrecognized event types are acknowledged without triggering subscription processing
- Duplicate event IDs are processed only once (idempotency)
- No-op behavior when incoming data matches current subscription state

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Tests/Billing/WebhookSignatureAndIdempotencyPropertyTests.cs` | Property-based tests covering Properties 1, 2, 3, 17, and 18 from the design document |

## Key decisions

- **Direct validator testing for signature properties**: Tests the `WebhookSignatureValidator` directly with generated payloads and secrets, computing expected HMAC-SHA256 signatures independently to verify soundness.
- **In-memory EF Core for idempotency tests**: Uses `UseInMemoryDatabase` to test the `HandleWebhookCommandHandler` idempotency logic without requiring a real database.
- **NSubstitute for MediatR verification**: Verifies that no sub-handlers are dispatched for duplicate events or unrecognized event types by checking `DidNotReceive()` on the mocked mediator.
- **Separate tests for valid/invalid/tampered signatures**: Property 1 is split into three complementary property tests for clarity.

## How it connects

- Tests validate `WebhookSignatureValidator` (Infrastructure layer)
- Tests validate `HandleWebhookCommandHandler` idempotency logic (Application layer)
- Tests validate `HandleSubscriptionUpdatedCommandHandler` no-op behavior (Application layer)
- Validates Requirements 2.1, 2.2, 2.5, 2.7, 10.1, 10.3

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~WebhookSignatureAndIdempotencyPropertyTests"
```

All 10 tests should pass (100 iterations each for property tests).

## What comes next

- Task 9.3: Unit tests for status mapping and configuration validation (Property 16)

## Git commit

```bash
git add -A && git commit -m "feat(billing): add webhook signature and idempotency property tests"
```
