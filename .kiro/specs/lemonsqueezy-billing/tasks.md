# Implementation Plan: LemonSqueezy Billing Integration

## Overview

Replace the existing Stripe billing integration with LemonSqueezy. This involves migrating the `GroupSubscription` entity fields, creating a new webhook controller, implementing checkout session creation via the LemonSqueezy API, adding idempotent webhook event processing, and wiring configuration/DI. The implementation follows the existing Clean Architecture pattern (Domain → Application → Infrastructure → Api).

## Tasks

- [x] 1. Domain layer: Migrate GroupSubscription entity and add WebhookEventLog
  - [x] 1.1 Replace Stripe identifiers with LemonSqueezy identifiers on GroupSubscription
    - Rename `StripeSubscriptionId` to `LemonSqueezySubscriptionId` in `Jobuler.Domain/Billing/GroupSubscription.cs`
    - Rename `StripeCustomerId` to `LemonSqueezyCustomerId`
    - Update the `Activate` method signature to accept `lemonSqueezySubscriptionId` and `lemonSqueezyCustomerId` parameters
    - Add `UpdateStatus(SubscriptionStatus newStatus)` method for webhook-driven status transitions
    - Add `UpdatePeriod(DateTime periodStart, DateTime periodEnd)` method that resets `PeakMemberCount` when period start changes
    - Update any existing callers of `Activate` in the Application layer to use the new parameter names
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 1.2 Create WebhookEventLog domain entity
    - Create `Jobuler.Domain/Billing/WebhookEventLog.cs`
    - Include properties: `EventId` (string), `EventType` (string), `ProcessedAt` (DateTime)
    - Add static factory method `Create(string eventId, string eventType)`
    - _Requirements: 10.1, 10.2_

- [x] 2. Infrastructure layer: Configuration, HTTP client, and database migration
  - [x] 2.1 Create LemonSqueezySettings configuration class
    - Create `Jobuler.Infrastructure/Billing/LemonSqueezySettings.cs`
    - Properties: `ApiKey`, `WebhookSecret`, `StoreId`, `DefaultVariantId`, `TestVariantId`
    - Add startup validation that throws if any required value is missing or whitespace-only, identifying the specific missing key
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [x] 2.2 Implement ILemonSqueezyClient HTTP client
    - Create `Jobuler.Application/Billing/ILemonSqueezyClient.cs` interface with `CreateCheckoutAsync` method
    - Create `Jobuler.Infrastructure/Billing/LemonSqueezyClient.cs` implementing the interface
    - Use `HttpClient` to call LemonSqueezy API for checkout session creation
    - Accept `CreateCheckoutRequest` record with `VariantId`, `Metadata` dictionary, and optional `CustomerEmail`
    - Return the checkout URL string
    - Handle API errors by throwing with the failure reason (no secrets exposed)
    - _Requirements: 1.1, 1.3, 9.1_

  - [x] 2.3 Implement IWebhookSignatureValidator
    - Create `Jobuler.Application/Billing/IWebhookSignatureValidator.cs` interface with `Verify(string payload, string signature)` method
    - Create `Jobuler.Infrastructure/Billing/WebhookSignatureValidator.cs` implementing HMAC-SHA256 verification using the configured webhook secret
    - _Requirements: 2.1, 2.2_

  - [x] 2.4 Create EF Core database migration
    - Add EF Core configuration for `WebhookEventLog` entity in `Jobuler.Infrastructure/Persistence/`
    - Rename `StripeSubscriptionId` column to `LemonSqueezySubscriptionId` on `group_subscriptions` table
    - Rename `StripeCustomerId` column to `LemonSqueezyCustomerId` on `group_subscriptions` table
    - Create `webhook_event_logs` table with unique index on `event_id` and index on `processed_at`
    - Generate and apply the EF Core migration
    - _Requirements: 7.1, 7.2, 10.2_

  - [x] 2.5 Register LemonSqueezy services in DI
    - Register `LemonSqueezySettings` from configuration section `"LemonSqueezy"`
    - Register `ILemonSqueezyClient` → `LemonSqueezyClient` as scoped
    - Register `IWebhookSignatureValidator` → `WebhookSignatureValidator` as singleton
    - Add startup validation call for `LemonSqueezySettings`
    - Wire up `HttpClient` for `LemonSqueezyClient` via `IHttpClientFactory`
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

- [x] 3. Checkpoint - Ensure infrastructure compiles and configuration validation works
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Application layer: Checkout command
  - [x] 4.1 Implement CreateCheckoutCommand and handler
    - Create `Jobuler.Application/Billing/Commands/CreateCheckoutCommand.cs` with `SpaceId`, `GroupId`, `UserId` properties
    - Create handler that: validates group exists and belongs to space, checks no active/trialing subscription exists, calls `ILemonSqueezyClient.CreateCheckoutAsync` with space/group metadata, returns checkout URL
    - Include `BillingManage` permission check
    - Return error if group already has active/trialing subscription
    - Return not-found if group doesn't exist or doesn't belong to space
    - _Requirements: 1.1, 1.2, 1.4, 1.5, 1.6, 1.7_

  - [x]* 4.2 Write property test: Checkout rejected for active/trialing subscriptions
    - **Property 19: Checkout is rejected for active or trialing subscriptions**
    - **Validates: Requirements 1.6**

- [x] 5. Application layer: Webhook event handlers
  - [x] 5.1 Implement HandleWebhookCommand for dispatch and idempotency
    - Create `Jobuler.Application/Billing/Commands/HandleWebhookCommand.cs` with `EventId`, `EventType`, `Payload` (JSON string), `Metadata` dictionary
    - Handler checks if `EventId` already exists in `WebhookEventLog` — if so, skip processing
    - Stores `EventId` in `WebhookEventLog` before dispatching to specific handler
    - Dispatches to appropriate sub-handler based on event type
    - If metadata contains `charge_type=test-charge`, log at Info level and skip subscription processing
    - For unrecognized event types, log and return without processing
    - _Requirements: 2.3, 2.7, 8.5, 10.1, 10.4_

  - [x] 5.2 Implement HandleSubscriptionCreatedCommand
    - Create handler for `subscription_created` events
    - Look up `GroupSubscription` by space/group from webhook metadata
    - If no subscription exists, log warning and skip
    - If subscription already has Active/Trialing status with a stored LemonSqueezy ID, treat as no-op
    - If status is "active": set Status=Active, store LemonSqueezy IDs, set period dates, set tier from product metadata
    - If status is "on_trial": set Status=Trialing, set TrialEndsAt from payload
    - If status is neither "active" nor "on_trial": log warning and skip
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 5.3 Implement HandleSubscriptionUpdatedCommand
    - Create handler for `subscription_updated` events
    - Look up `GroupSubscription` by LemonSqueezy subscription ID
    - Map LemonSqueezy status to `SubscriptionStatus` enum (active→Active, on_trial→Trialing, past_due→PastDue, cancelled→Canceled, expired→Expired)
    - If status is unrecognized, log warning and skip
    - If incoming status and period dates match current values, treat as no-op (no DB write, no audit log)
    - Update period dates if they differ from stored values
    - If transitioning to Active from Trialing/PastDue/Canceled/Expired, reactivate the associated group
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 10.3_

  - [x] 5.4 Implement HandleSubscriptionCancelledCommand
    - Create handler for `subscription_cancelled` events
    - Look up `GroupSubscription` by LemonSqueezy subscription ID
    - If status is already Canceled or Expired, treat as no-op
    - Set Status=Canceled and CanceledAt=DateTime.UtcNow
    - If `CurrentPeriodEnd` is null or in the past, deactivate the group immediately
    - If `CurrentPeriodEnd` is in the future, leave group active (deferred to ExpireSubscriptionsJob)
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 5.5 Implement HandlePaymentSuccessCommand
    - Create handler for `subscription_payment_success` events
    - Look up `GroupSubscription` by LemonSqueezy subscription ID
    - If no subscription found, log warning and skip
    - Update billing period to new dates from payload
    - If new period start differs from current, reset PeakMemberCount to 0
    - If subscription is in PastDue status, transition to Active
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x]* 5.6 Write property tests for subscription event handlers
    - **Property 4: Subscription creation maps to correct entity state**
    - **Property 5: Already-activated subscription ignores duplicate creation events**
    - **Property 6: Unrecognized creation statuses are skipped**
    - **Property 7: Unrecognized update statuses are skipped**
    - **Property 8: Period dates are updated when they differ**
    - **Property 9: Transition to Active reactivates the group**
    - **Property 10: Cancellation sets status and timestamp**
    - **Property 11: Cancellation deactivates group if and only if period has ended**
    - **Property 12: Already-canceled subscriptions ignore cancellation events**
    - **Property 13: Payment success updates period and conditionally resets peak**
    - **Property 14: Payment success transitions PastDue to Active**
    - **Property 15: Test charges never modify subscriptions**
    - **Validates: Requirements 3.1, 3.2, 3.4, 3.5, 4.3, 4.4, 4.5, 5.1, 5.2, 5.3, 5.4, 6.1, 6.2, 6.3, 8.5**

- [x] 6. Checkpoint - Ensure application layer compiles and handlers are correct
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. API layer: Webhook controller and billing endpoints
  - [x] 7.1 Create LemonSqueezyWebhookController
    - Create `Jobuler.Api/Controllers/LemonSqueezyWebhookController.cs`
    - Route: `POST /webhooks/lemonsqueezy`
    - Mark with `[AllowAnonymous]` (LemonSqueezy cannot provide bearer tokens)
    - Read raw request body
    - Verify signature via `IWebhookSignatureValidator` — return 401 if invalid, log failed attempt
    - Parse JSON payload — return 400 if malformed, log details
    - Extract event ID and event type
    - Dispatch to `HandleWebhookCommand` via MediatR
    - Always return 200 for successfully dispatched events
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

  - [x] 7.2 Add checkout endpoint to BillingController
    - Add `POST /spaces/{spaceId}/billing/groups/{groupId}/checkout` endpoint
    - Require `[Authorize]` and `BillingManage` permission
    - Dispatch `CreateCheckoutCommand` via MediatR
    - Return checkout URL in response body
    - _Requirements: 1.1, 1.4, 1.5_

  - [x] 7.3 Add test-charge endpoint to BillingController
    - Add `POST /spaces/{spaceId}/billing/test-charge` endpoint
    - Require `[Authorize]` and `BillingManage` permission
    - Create checkout session using `TestVariantId` from settings
    - Include metadata key `charge_type` with value `test-charge`
    - Return checkout URL in response body
    - _Requirements: 8.1, 8.2, 8.3, 8.4_

  - [x]* 7.4 Write property tests for webhook signature verification and idempotency
    - **Property 1: Webhook signature verification is sound**
    - **Property 2: Malformed payloads are rejected**
    - **Property 3: Unrecognized event types are acknowledged without processing**
    - **Property 17: Duplicate event IDs are idempotent**
    - **Property 18: No-op when incoming data matches current state**
    - **Validates: Requirements 2.1, 2.2, 2.5, 2.7, 10.1, 10.3**

- [x] 8. Checkpoint - Ensure full API compiles and endpoints are wired
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Integration wiring and cleanup
  - [x] 9.1 Update existing billing commands to use LemonSqueezy identifiers
    - Update `ActivateSubscriptionCommand` handler to pass LemonSqueezy IDs to `GroupSubscription.Activate`
    - Update `ExpireSubscriptionsCommand` handler if it references Stripe fields
    - Update `RenewSubscriptionCommand` handler if it references Stripe fields
    - Remove any remaining Stripe-specific code or references
    - _Requirements: 7.3, 7.4_

  - [x] 9.2 Add LemonSqueezy configuration to appsettings
    - Add `"LemonSqueezy"` section to `appsettings.json` with placeholder values
    - Add real development values to `appsettings.Development.json` (or document env vars)
    - Ensure `.env.example` includes LemonSqueezy keys if env-var approach is used
    - _Requirements: 9.1, 9.2, 9.3_

  - [x]* 9.3 Write unit tests for status mapping and configuration validation
    - **Property 16: Missing configuration prevents startup**
    - Test all 5 LemonSqueezy status → SubscriptionStatus mappings
    - Test checkout metadata includes spaceId and groupId
    - Test test-charge metadata includes `charge_type=test-charge`
    - Test webhook endpoint is `[AllowAnonymous]`
    - Test permission checks on checkout and test-charge endpoints
    - **Validates: Requirements 4.2, 8.4, 9.4**

- [x] 10. Final checkpoint - Ensure all tests pass and integration is complete
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck + xUnit
- Unit tests validate specific examples and edge cases
- The existing `ExpireSubscriptionsJob` handles deferred expiration of canceled subscriptions — no new job needed
- The webhook controller is `[AllowAnonymous]` per requirement 2.6 and security is enforced via HMAC signature verification

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["2.4", "2.5"] },
    { "id": 3, "tasks": ["4.1", "5.1"] },
    { "id": 4, "tasks": ["4.2", "5.2", "5.3", "5.4", "5.5"] },
    { "id": 5, "tasks": ["5.6", "7.1", "7.2", "7.3"] },
    { "id": 6, "tasks": ["7.4", "9.1", "9.2"] },
    { "id": 7, "tasks": ["9.3"] }
  ]
}
```
