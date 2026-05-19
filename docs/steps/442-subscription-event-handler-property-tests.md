# 442 — Subscription Event Handler Property Tests

## Phase
LemonSqueezy Billing Integration — Application Layer Testing

## Purpose
Validates the correctness of subscription event handlers (created, updated, cancelled, payment success) through property-based tests. These tests ensure that webhook-driven state transitions behave correctly across all valid inputs, covering Properties 4–15 from the design document.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Billing/SubscriptionEventHandlerPropertyTests.cs` | 15 FsCheck property tests covering subscription creation, update, cancellation, payment success, and test-charge isolation |

## Key decisions
- Tests exercise the actual handler classes with an in-memory EF Core database rather than mocking the DbContext, giving higher confidence in real behavior
- Reflection is used to set Group entity IDs to match subscription GroupIds for handler lookup queries
- Each property test generates 100 random inputs using FsCheck generators for dates, subscription IDs, and statuses
- Test-charge isolation (Property 15) verifies via NSubstitute that no sub-handler is dispatched when `charge_type=test-charge` metadata is present

## How it connects
- Tests validate handlers implemented in tasks 5.1–5.5 (HandleWebhookCommand, HandleSubscriptionCreatedCommand, HandleSubscriptionUpdatedCommand, HandleSubscriptionCancelledCommand, HandlePaymentSuccessCommand)
- Complements the existing `SubscriptionLifecyclePropertyTests` (domain-level) and `SubscriptionApplicationPropertyTests` (application-level) with webhook handler coverage
- Validates requirements 3.1, 3.2, 3.4, 3.5, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 8.5

## How to run / verify
```bash
cd apps/api
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~SubscriptionEventHandlerPropertyTests"
```

All 15 tests should pass.

## What comes next
- Task 7.4: Property tests for webhook signature verification and idempotency
- Task 9.3: Unit tests for status mapping and configuration validation

## Git commit
```bash
git add -A && git commit -m "feat(billing): subscription event handler property tests (Properties 4-15)"
```
