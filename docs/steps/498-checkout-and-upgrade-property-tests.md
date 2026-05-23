# Step 498 — Checkout and Upgrade Property Tests

## Phase

Phase: Space-Level Billing — Property-Based Testing

## Purpose

Validates correctness properties for the space-level checkout and upgrade commands using FsCheck property-based testing. Ensures that:
1. Checkout metadata always includes `space_id` (Property 6)
2. Active subscriptions reject checkout and never call LemonSqueezy (Property 7)
3. Non-active/non-trialing subscriptions reject upgrade (Property 16)

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Tests/Billing/CheckoutAndUpgradePropertyTests.cs` | FsCheck property tests for Properties 6, 7, and 16 with 100 iterations each |

## Key decisions

- Used NSubstitute with a capturing pattern for Property 6 to verify the metadata dictionary passed to `CreateCheckoutAsync`
- Used `DidNotReceive()` assertions for Properties 7 and 16 to verify LemonSqueezy is never called on rejection
- Used reflection to set `PastDue` status since the domain entity doesn't expose a direct transition method for it
- Each property test uses a unique in-memory database name to avoid cross-test interference

## How it connects

- Tests validate `CreateSpaceCheckoutCommandHandler` and `UpgradeSpacePlanCommandHandler` from the Application layer
- References `SpaceSubscription` domain entity state transitions
- Validates Requirements 5.1, 5.2, 9.4, 10.2 from the space-billing spec

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~CheckoutAndUpgradePropertyTests" --verbosity normal
```

All 3 property tests should pass (100 iterations each).

## What comes next

- Task 6.5: Property tests for webhook handling (Properties 2, 8)
- Task 9.2: Property test for migration correctness (Property 14)

## Git commit

```bash
git add -A && git commit -m "feat(billing): add property tests for checkout and upgrade commands (Properties 6, 7, 16)"
```
