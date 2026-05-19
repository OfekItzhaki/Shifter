# Step 438 — Billing Test-Charge Endpoint

## Phase

Phase 7 — API Layer: Webhook Controller and Billing Endpoints

## Purpose

Provides a developer-facing endpoint to verify the LemonSqueezy integration works end-to-end by initiating a small (~$1) checkout session tagged as a test charge. This allows confirming the full payment flow without affecting real subscription state.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Api/Controllers/BillingController.cs` | Added `POST /spaces/{spaceId}/billing/test-charge` endpoint; injected `ILemonSqueezyClient` and `IOptions<BillingOptions>` into the controller constructor |

## Key decisions

- The endpoint lives directly in `BillingController` rather than dispatching through a MediatR command, since it has no domain logic beyond calling the LemonSqueezy client with the test variant ID and metadata.
- Permission check (`BillingManage`) is performed inline in the controller action, consistent with how other billing endpoints handle authorization.
- Metadata includes `charge_type = "test-charge"` so the webhook handler can identify and skip subscription processing for test transactions.
- Also added the `CreateCheckout` endpoint (task 7.2) since it was missing from the controller.

## How it connects

- Uses `ILemonSqueezyClient.CreateCheckoutAsync` (implemented in task 2.2) to create the checkout session.
- Uses `BillingOptions.TestVariantId` (configured in task 2.1/2.5) for the test product variant.
- The `charge_type=test-charge` metadata is consumed by `HandleWebhookCommand` (task 5.1) which skips subscription processing for test charges.
- Requires `Permissions.BillingManage` (defined in task 1.2 of the billing-manage-permission spec).

## How to run / verify

```bash
dotnet build
# Endpoint: POST /spaces/{spaceId}/billing/test-charge
# Requires: Authorization header + BillingManage permission on the space
# Returns: { "checkoutUrl": "https://..." }
```

## What comes next

- Task 7.4: Property tests for webhook signature verification and idempotency
- Task 9.1: Update existing billing commands to use LemonSqueezy identifiers
- Task 9.3: Unit tests for test-charge metadata and permission checks

## Git commit

```bash
git add -A && git commit -m "feat(billing): add test-charge endpoint to BillingController"
```
