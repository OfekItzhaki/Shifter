# Step 499 — Webhook Handling Property Tests (Properties 2, 8)

## Phase

Space-Level Billing — Application Layer Testing

## Purpose

Validates two correctness properties for webhook handling:
- **Property 2 (Subscription creation idempotency)**: Ensures that processing a `subscription_created` webhook for a space that already has an active subscription does not create a second subscription or modify the existing one.
- **Property 8 (Webhook idempotency)**: Ensures that processing a webhook with a duplicate event ID (already in `WebhookEventLog`) does not dispatch any sub-handlers or modify `SpaceSubscription` state.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Billing/WebhookHandlingPropertyTests.cs` | FsCheck property tests (100 iterations each) for Properties 2 and 8 |

## Key decisions

- Used in-memory EF Core database for isolation between test runs (unique DB name per iteration).
- Property 2 tests the `HandleSpaceSubscriptionCreatedCommandHandler` directly — first call activates, second call with different payload is a no-op.
- Property 8 tests the `HandleWebhookCommandHandler` — first call logs the event and dispatches, second call with same event ID returns early without dispatching.
- Used NSubstitute for `IMediator` and `IStatisticsPeriodService` to verify no side effects on duplicate processing.
- Generators constrain inputs to valid ranges (non-empty GUIDs, positive trial days, reasonable period durations).

## How it connects

- Validates Requirements 1.6 (no duplicate subscriptions) and 5.3 (webhook idempotency via `WebhookEventLog`).
- Tests the handlers implemented in tasks 6.1 and 6.4.
- Complements the existing `WebhookSignatureAndIdempotencyPropertyTests.cs` which tests group-level webhook idempotency.

## How to run / verify

```bash
dotnet test apps/api/Jobuler.Tests --filter "FullyQualifiedName~WebhookHandlingPropertyTests" --verbosity normal
```

Both properties should pass with 100 iterations each.

## What comes next

- Task 9.2: Migration correctness property test (Property 14)
- Task 10.3: Webhook signature rejection property test (Property 17)

## Git commit

```bash
git add -A && git commit -m "feat(billing): add webhook handling property tests (Properties 2, 8)"
```
