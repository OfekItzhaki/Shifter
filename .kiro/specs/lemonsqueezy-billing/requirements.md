# Requirements Document

## Introduction

This feature replaces the existing Stripe billing integration with LemonSqueezy as the payment provider for the Shifter app. The integration covers the full billing lifecycle: checkout session creation, webhook handling for subscription events, subscription state management, and a test endpoint for end-to-end payment verification. The existing `GroupSubscription` entity fields (`StripeSubscriptionId`, `StripeCustomerId`) will be replaced with LemonSqueezy equivalents.

## Glossary

- **Billing_Service**: The backend service responsible for communicating with the LemonSqueezy API and managing subscription state in the database.
- **Webhook_Handler**: The API endpoint that receives and processes incoming webhook events from LemonSqueezy.
- **Checkout_Session**: A server-initiated request to LemonSqueezy that returns a URL where the user completes payment.
- **LemonSqueezy**: The third-party payment provider that hosts checkout pages and manages subscription billing.
- **Group_Subscription**: The domain entity representing a group's billing subscription, scoped to a space and group.
- **Subscription_Status**: The enum representing the lifecycle state of a subscription (Trialing, Active, PastDue, Canceled, Expired).
- **Test_Endpoint**: A development/verification endpoint that initiates a ~$1 charge to confirm the integration works end-to-end.
- **Webhook_Signature**: The HMAC signature included in LemonSqueezy webhook requests used to verify authenticity.

## Requirements

### Requirement 1: Create Checkout Session

**User Story:** As a space admin, I want to initiate a LemonSqueezy checkout session for my group, so that I can subscribe to a paid plan.

#### Acceptance Criteria

1. WHEN a space admin requests a checkout session for a group, THE Billing_Service SHALL validate that the group exists and belongs to the specified space, create a checkout session via the LemonSqueezy API, and return the checkout URL to the caller.
2. THE Billing_Service SHALL include the space ID and group ID as custom metadata in the checkout session so the webhook can associate the payment with the correct group.
3. IF the LemonSqueezy API returns an error during checkout creation, THEN THE Billing_Service SHALL return an error response indicating the checkout could not be created, including the failure reason from LemonSqueezy, without exposing internal API keys or secrets.
4. THE Billing_Service SHALL require the `BillingManage` permission before creating a checkout session.
5. THE Billing_Service SHALL scope the checkout session request to the authenticated user's space and group (tenant isolation).
6. IF the group already has an active or trialing subscription, THEN THE Billing_Service SHALL reject the checkout request with an error response indicating that the group already has an active subscription.
7. IF the specified group does not exist or does not belong to the authenticated user's space, THEN THE Billing_Service SHALL reject the request with a not-found error response.

### Requirement 2: Webhook Reception and Verification

**User Story:** As the system, I want to securely receive and verify LemonSqueezy webhook events, so that subscription state stays synchronized with the payment provider.

#### Acceptance Criteria

1. WHEN a webhook request is received, THE Webhook_Handler SHALL verify the request signature using the configured webhook secret before processing the payload.
2. IF the webhook signature is invalid, THEN THE Webhook_Handler SHALL reject the request with HTTP 401 and log the failed verification attempt.
3. WHEN a webhook request has a valid signature, THE Webhook_Handler SHALL parse the event type and dispatch it to the appropriate handler.
4. WHEN a webhook request is received, THE Webhook_Handler SHALL return HTTP 200 to LemonSqueezy within 5 seconds to acknowledge receipt, even if downstream processing is deferred.
5. IF the webhook payload cannot be parsed, THEN THE Webhook_Handler SHALL return HTTP 400 and log the malformed payload details for debugging.
6. THE Webhook_Handler SHALL be accessible without authentication (public endpoint) since LemonSqueezy cannot provide a bearer token.
7. IF a webhook event has a valid signature but contains an unrecognized event type, THEN THE Webhook_Handler SHALL log the event type, return HTTP 200, and skip further processing.

### Requirement 3: Subscription Created Event Handling

**User Story:** As the system, I want to activate a group subscription when LemonSqueezy confirms a new subscription, so that the group gains access to paid features.

#### Acceptance Criteria

1. WHEN a `subscription_created` webhook event is received with status "active", THE Billing_Service SHALL activate the Group_Subscription by setting its status to Active, storing the LemonSqueezy subscription ID and customer ID, setting the tier ID from the webhook product/variant metadata, and recording the billing period start and end dates from the webhook payload.
2. WHEN a `subscription_created` webhook event is received with status "on_trial", THE Billing_Service SHALL set the Group_Subscription status to Trialing and update the trial end date to the trial period end date provided in the LemonSqueezy webhook payload.
3. IF no Group_Subscription exists for the space and group specified in the webhook metadata, THEN THE Billing_Service SHALL log a warning and skip processing without returning an error to LemonSqueezy.
4. IF a `subscription_created` webhook event is received but the Group_Subscription already has an active or trialing status with a stored LemonSqueezy subscription ID, THEN THE Billing_Service SHALL treat the event as a no-op and return HTTP 200.
5. IF a `subscription_created` webhook event is received with a status other than "active" or "on_trial", THEN THE Billing_Service SHALL log a warning including the unexpected status value and skip activation.

### Requirement 4: Subscription Updated Event Handling

**User Story:** As the system, I want to update subscription state when LemonSqueezy reports changes, so that the app reflects the current billing status.

#### Acceptance Criteria

1. WHEN a `subscription_updated` webhook event is received, THE Billing_Service SHALL map the LemonSqueezy status to the corresponding Subscription_Status value and update the Group_Subscription status accordingly.
2. THE Billing_Service SHALL map LemonSqueezy statuses as follows: "active" maps to Active, "on_trial" maps to Trialing, "past_due" maps to PastDue, "cancelled" maps to Canceled, "expired" maps to Expired.
3. IF a `subscription_updated` webhook event contains a LemonSqueezy status not present in the defined mapping, THEN THE Billing_Service SHALL log a warning including the unrecognized status value and skip the status update without returning an error to LemonSqueezy.
4. WHEN the billing period start or end dates in the `subscription_updated` webhook payload differ from the currently stored CurrentPeriodStart or CurrentPeriodEnd values, THE Billing_Service SHALL update the Group_Subscription with the new period dates from the payload.
5. WHEN a `subscription_updated` event transitions the Group_Subscription status from Trialing, PastDue, Canceled, or Expired to Active, THE Billing_Service SHALL reactivate the associated group by restoring its active state if it was previously deactivated.

### Requirement 5: Subscription Cancelled Event Handling

**User Story:** As the system, I want to handle subscription cancellation events from LemonSqueezy, so that cancelled groups are properly marked and eventually expired.

#### Acceptance Criteria

1. WHEN a `subscription_cancelled` webhook event is received, THE Billing_Service SHALL set the Group_Subscription status to Canceled and record the current UTC timestamp as the cancellation date.
2. WHEN a `subscription_cancelled` event is received and the Group_Subscription CurrentPeriodEnd is in the past or null, THE Billing_Service SHALL deactivate the associated group immediately.
3. WHEN a `subscription_cancelled` event is received and the Group_Subscription CurrentPeriodEnd is in the future, THE Billing_Service SHALL retain the group in active state without deactivation, deferring expiration to the ExpireSubscriptionsJob which runs every 6 hours and expires canceled subscriptions past their billing period.
4. IF a `subscription_cancelled` event is received but the Group_Subscription status is already Canceled or Expired, THEN THE Billing_Service SHALL treat the event as a no-op and return HTTP 200.

### Requirement 6: Payment Success Event Handling

**User Story:** As the system, I want to track successful payments, so that subscription periods are renewed and billing records stay accurate.

#### Acceptance Criteria

1. WHEN a `subscription_payment_success` webhook event is received, THE Billing_Service SHALL update the Group_Subscription billing period to the new period start and end dates from the payment.
2. WHEN a payment succeeds for a subscription in PastDue status, THE Billing_Service SHALL transition the Group_Subscription status to Active.
3. WHEN a `subscription_payment_success` webhook event is received and the new period start date differs from the current period start date, THE Billing_Service SHALL reset the peak member count to 0.
4. IF a `subscription_payment_success` webhook event is received but no Group_Subscription exists for the LemonSqueezy subscription ID in the payload, THEN THE Billing_Service SHALL log a warning and skip processing without returning an error to the webhook caller.

### Requirement 7: Entity Migration from Stripe to LemonSqueezy

**User Story:** As a developer, I want the GroupSubscription entity to use LemonSqueezy identifiers instead of Stripe identifiers, so that the domain model reflects the actual payment provider.

#### Acceptance Criteria

1. THE Group_Subscription entity SHALL have a `LemonSqueezySubscriptionId` property (string, nullable) replacing `StripeSubscriptionId`.
2. THE Group_Subscription entity SHALL have a `LemonSqueezyCustomerId` property (string, nullable) replacing `StripeCustomerId`.
3. THE Billing_Service SHALL use the LemonSqueezy identifiers when looking up or updating subscriptions from webhook events.
4. THE Group_Subscription `Activate` method SHALL accept LemonSqueezy identifiers (subscription ID and customer ID) instead of Stripe identifiers as parameters alongside tier ID and period dates.

### Requirement 8: Test Charge Endpoint

**User Story:** As a developer, I want a test endpoint that charges approximately $1, so that I can verify the LemonSqueezy integration works end-to-end with real money.

#### Acceptance Criteria

1. WHEN the test charge endpoint is called, THE Billing_Service SHALL create a LemonSqueezy checkout session for a pre-configured test product priced between $0.50 and $2.00 (configured via environment settings).
2. THE Billing_Service SHALL return the checkout URL in the response body so the developer can complete the payment manually.
3. THE Test_Endpoint SHALL require authentication and the `BillingManage` permission.
4. THE Test_Endpoint SHALL include a metadata key `charge_type` with value `test-charge` in the checkout session so test transactions are distinguishable from real subscriptions in webhook processing.
5. WHEN a webhook is received where the metadata `charge_type` equals `test-charge`, THE Billing_Service SHALL log the successful payment at Info level and SHALL NOT activate or modify any Group_Subscription.

### Requirement 9: Configuration Management

**User Story:** As a developer, I want LemonSqueezy API credentials and product IDs stored securely in configuration, so that the integration works across environments without hardcoded secrets.

#### Acceptance Criteria

1. THE Billing_Service SHALL read the LemonSqueezy API key from environment configuration (not hardcoded in source).
2. THE Billing_Service SHALL read the webhook signing secret from environment configuration.
3. THE Billing_Service SHALL read the store ID and product/variant IDs from environment configuration.
4. IF any required LemonSqueezy configuration value is missing or empty (whitespace-only) at startup, THEN THE Billing_Service SHALL fail to start and produce an error message identifying which specific configuration value is missing.

### Requirement 10: Idempotent Webhook Processing

**User Story:** As the system, I want webhook processing to be idempotent, so that duplicate webhook deliveries do not corrupt subscription state.

#### Acceptance Criteria

1. WHEN a webhook event is received that has already been processed (same event ID), THE Webhook_Handler SHALL skip processing and return HTTP 200.
2. THE Webhook_Handler SHALL store processed webhook event IDs for a minimum of 7 days to detect duplicates.
3. WHEN a `subscription_updated` event arrives with a status and period dates that match the current Group_Subscription values, THE Billing_Service SHALL treat the operation as a no-op and not produce duplicate audit log entries.
4. IF two webhook events with the same event ID arrive concurrently, THEN THE Webhook_Handler SHALL ensure only one is processed and the other returns HTTP 200 without processing.
