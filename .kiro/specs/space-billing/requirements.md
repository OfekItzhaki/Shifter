# Requirements Document

## Introduction

This feature migrates billing from per-group subscriptions to per-space subscriptions. A single subscription at the space level covers all groups within that space. Trial duration is sourced from LemonSqueezy (not hardcoded). Subscription boundaries (trial end, subscription expiry without renewal) define statistics period boundaries. The existing TrialBanner component is retained but rewired to check space-level subscription status.

## Glossary

- **Space**: The top-level organizational entity that contains one or more Groups. Each Space has exactly one subscription.
- **Group**: A scheduling unit within a Space. Groups no longer have their own subscriptions.
- **Space_Subscription**: The billing record associated with a Space, tracking trial and paid subscription state via LemonSqueezy.
- **Billing_Service**: The backend application service responsible for managing Space_Subscription lifecycle (creation, activation, cancellation, expiry).
- **LemonSqueezy_Client**: The infrastructure component that communicates with the LemonSqueezy API to create checkouts and retrieve subscription details.
- **Trial_Banner**: The frontend component that displays trial/subscription status to users on group pages.
- **Space_Settings_Page**: The frontend page where space administrators view and manage subscription details.
- **Statistics_Period**: A time range bounded by subscription lifecycle events (trial start, trial end, subscription start, subscription expiry without renewal).
- **Admin**: A user with the BillingManage permission for a given Space.

## Requirements

### Requirement 1: Space Subscription Creation on Space Creation

**User Story:** As a space owner, I want a trial subscription to be automatically created when I create a space, so that I can start using the platform immediately.

#### Acceptance Criteria

1. WHEN a Space is created, THE Billing_Service SHALL create exactly one Space_Subscription with status "trialing" for that Space.
2. WHEN a Space_Subscription is created, THE Billing_Service SHALL retrieve the trial duration from the locally cached LemonSqueezy product variant configuration.
3. WHEN a Space_Subscription is created, THE Billing_Service SHALL record the trial start date as the current UTC timestamp.
4. WHEN a Space_Subscription is created, THE Billing_Service SHALL compute the trial end date as the trial start date plus the trial duration from the cached configuration.
5. IF the locally cached trial duration is unavailable, THEN THE Billing_Service SHALL fall back to a default trial duration of 14 days.
6. IF a Space_Subscription already exists for a Space, THEN THE Billing_Service SHALL not create a duplicate Space_Subscription and SHALL leave the existing record unchanged.
7. THE Billing_Service SHALL periodically sync the trial duration from the LemonSqueezy product variant configuration and store it locally for fallback use.

### Requirement 2: Space Subscription Covers All Groups

**User Story:** As a space owner, I want one subscription to cover all groups in my space, so that I do not need to manage billing per group.

#### Acceptance Criteria

1. WHEN the Billing_Service evaluates access for any Group within a Space, THE Billing_Service SHALL use the Space_Subscription status of that Space as the sole billing authority for the Group.
2. WHEN a new Group is added to a Space with a Space_Subscription in status "active" or "trialing", THE Billing_Service SHALL grant that Group access to all subscription-tier features without requiring a separate subscription or additional checkout.
3. IF the Space_Subscription status is "canceled", "expired", or "past_due", THEN THE Billing_Service SHALL deny access to premium features for all Groups in that Space and return a response indicating the subscription is inactive.
4. WHEN a new Group is added to a Space whose Space_Subscription status is "canceled", "expired", or "past_due", THE Billing_Service SHALL create the Group but deny access to premium features until the Space_Subscription is reactivated.

### Requirement 3: Trial Banner Displays Space Subscription Status

**User Story:** As a group member, I want to see the trial status of my space on group pages, so that I know how much trial time remains.

#### Acceptance Criteria

1. WHILE the Space_Subscription status is "trialing", THE Trial_Banner SHALL display the number of days remaining until trial expiry, calculated as the ceiling of the difference between the trial end date and the current date (minimum 0).
2. IF the Space_Subscription status is "trialing" and the computed days remaining is 0, THEN THE Trial_Banner SHALL display a message prompting the user to upgrade and provide a navigation action to the pricing page.
3. WHILE the Space_Subscription status is "active" and auto-renewing, THE Trial_Banner SHALL remain hidden.
4. WHILE the Space_Subscription status is "active" and the subscription is not auto-renewing and the subscription end date is within 7 days, THE Trial_Banner SHALL display a message indicating the number of days until subscription expiry.
5. IF the Space_Subscription data fails to load, THEN THE Trial_Banner SHALL remain hidden.
6. THE Trial_Banner SHALL use the same color scheme as the existing per-group trial banner (sky for >7 days, amber for 4-7 days, red for ≤3 days).

### Requirement 4: Space Settings Subscription Display

**User Story:** As a space admin, I want to see subscription start and end dates on the space settings page, so that I can track my billing period.

#### Acceptance Criteria

1. WHEN an Admin navigates to the Space_Settings_Page, THE Space_Settings_Page SHALL display the current subscription status as one of: "trialing", "active", "past_due", "canceled", or "expired".
2. WHILE the Space_Subscription status is "trialing", THE Space_Settings_Page SHALL display the trial start date (subscription creation date) and trial end date in the format YYYY-MM-DD.
3. WHILE the Space_Subscription status is "active", THE Space_Settings_Page SHALL display the current period start date and current period end date in the format YYYY-MM-DD.
4. WHILE the Space_Subscription status is "canceled", THE Space_Settings_Page SHALL display the current period end date as the access expiry date and the cancellation date.
5. IF the Admin navigates to the Space_Settings_Page and no Space_Subscription exists for the Space, THEN THE Space_Settings_Page SHALL display a message indicating no subscription is associated with the Space.
6. THE Space_Settings_Page SHALL restrict access to subscription details to users with the BillingManage permission for the Space.

### Requirement 5: Subscription Checkout at Space Level

**User Story:** As a space admin, I want to upgrade my space subscription through a checkout flow, so that I can unlock premium features for all groups.

#### Acceptance Criteria

1. WHEN an Admin initiates a checkout, THE Billing_Service SHALL verify the Admin holds the BillingManage permission for the Space and create a LemonSqueezy checkout session with the Space ID included in the checkout metadata.
2. IF the Space_Subscription status is already "active" when an Admin initiates a checkout, THEN THE Billing_Service SHALL reject the request with an error indicating the space already has an active subscription.
3. WHEN the Billing_Service receives a subscription_created webhook, THE Billing_Service SHALL verify the webhook signature, check for duplicate event IDs to ensure idempotent processing, and then set the Space_Subscription status to "active" with the period start date and period end date extracted from the LemonSqueezy webhook payload.
4. IF the webhook signature verification fails, THEN THE Billing_Service SHALL reject the webhook with an unauthorized response and not modify the Space_Subscription.
5. IF the checkout fails or is abandoned, THEN THE Billing_Service SHALL leave the Space_Subscription status and period dates unchanged.

### Requirement 6: Subscription Lifecycle Management

**User Story:** As a space admin, I want to cancel or renew my subscription, so that I have control over my billing.

#### Acceptance Criteria

1. WHEN an Admin cancels the Space_Subscription while its status is "active" or "trialing", THE Billing_Service SHALL set the status to "canceled" and record the cancellation timestamp as the current UTC time.
2. IF an Admin attempts to cancel a Space_Subscription that is already "canceled" or "expired", THEN THE Billing_Service SHALL reject the request with an error indicating the subscription is already canceled or expired.
3. WHILE the Space_Subscription status is "canceled", THE Billing_Service SHALL maintain premium feature access for all Groups in the Space until the current period end date.
4. WHEN the current period end date passes for a canceled Space_Subscription, THE Billing_Service SHALL set the status to "expired" and restrict premium features for all Groups in the Space.
5. WHEN an Admin renews a Space_Subscription that is "canceled" and the current period end date has not yet passed, THE Billing_Service SHALL set the status to "active", clear the cancellation timestamp, and preserve the existing period dates.
6. WHEN an Admin renews a Space_Subscription that is "expired" or "canceled" past the current period end date, THE Billing_Service SHALL set the status to "active", clear the cancellation timestamp, and set a new period starting from the current UTC time.
7. IF an Admin attempts to renew a Space_Subscription that is already "active", THEN THE Billing_Service SHALL reject the request with an error indicating the subscription is already active.
8. WHEN the Billing_Service receives a subscription_updated webhook, THE Billing_Service SHALL update the Space_Subscription current period start date and current period end date to match the values provided in the webhook payload.

### Requirement 7: Statistics Period Boundaries

**User Story:** As a space admin, I want subscription lifecycle events to define statistics periods, so that I can track performance within billing cycles.

#### Acceptance Criteria

1. WHEN a Space_Subscription trial starts, THE Billing_Service SHALL open a new Statistics_Period (with status "active" and start boundary set to the trial start date) for each Group currently in the Space.
2. WHEN a Space_Subscription trial expires without activation, THE Billing_Service SHALL close the active Statistics_Period (setting status to "closed" and end boundary to the trial end date) for each Group in the Space.
3. WHEN a Space_Subscription is activated, THE Billing_Service SHALL close the existing active Statistics_Period for each Group in the Space and open a new Statistics_Period with start boundary set to the subscription activation date.
4. WHEN a Space_Subscription expires without renewal, THE Billing_Service SHALL close the active Statistics_Period (setting status to "closed" and end boundary to the subscription end date) for each Group in the Space.
5. WHEN the Billing_Service receives a subscription_updated webhook indicating a new billing period, THE Billing_Service SHALL close the active Statistics_Period for each Group in the Space and open a new Statistics_Period with start boundary set to the new period start date.
6. IF no Groups exist in the Space at the time of a subscription lifecycle event, THEN THE Billing_Service SHALL skip Statistics_Period creation and log the event for reconciliation when a Group is later added.

### Requirement 8: Migration from Group-Level Billing

**User Story:** As a platform operator, I want to migrate from group-level billing to space-level billing, so that the system uses the new billing model.

#### Acceptance Criteria

1. WHEN the migration runs, THE Billing_Service SHALL set the status of all existing GroupSubscription records to "migrated" without deleting or modifying any other fields on those records.
2. WHEN the migration runs and a Space contains at least one GroupSubscription with status "active" or "trialing", THE Billing_Service SHALL create a Space_Subscription for that Space with status "active" and period dates carried over from the GroupSubscription with the latest CurrentPeriodEnd.
3. WHEN the migration runs and a Space has no GroupSubscription with status "active" or "trialing", THE Billing_Service SHALL create a Space_Subscription with status "trialing" and a trial duration retrieved from the LemonSqueezy product variant configuration.
4. IF a Space already has a Space_Subscription when the migration runs, THEN THE Billing_Service SHALL skip that Space without modifying the existing Space_Subscription.
5. IF the migration fails partway through, THEN THE Billing_Service SHALL roll back all changes within the current batch, leaving already-completed batches and unprocessed records unchanged, and log an error indicating the failure point.
6. WHEN the migration completes successfully, THE Billing_Service SHALL route all new billing operations (checkout, cancel, renew, webhook handling) through the Space_Subscription model and reject any billing operation targeting a GroupSubscription directly.

### Requirement 9: LemonSqueezy Integration Verification

**User Story:** As a platform operator, I want to verify the LemonSqueezy integration works end-to-end, so that billing operations are reliable.

#### Acceptance Criteria

1. WHEN an Admin triggers a test charge, THE LemonSqueezy_Client SHALL create a checkout session using the test variant ID and include the Space ID in the checkout metadata.
2. WHEN the LemonSqueezy_Client receives a 401 response, THE Billing_Service SHALL log an error indicating that the LemonSqueezy API key authentication failed and SHALL throw an exception that results in an error response to the caller.
3. IF the LemonSqueezy API does not respond within 10 seconds or the connection is refused, THEN THE Billing_Service SHALL return an error response indicating the billing provider is unavailable.
4. THE LemonSqueezy_Client SHALL include the Space ID in all checkout metadata for webhook correlation.
5. IF the LemonSqueezy_Client receives a 4xx or 5xx response other than 401, THEN THE Billing_Service SHALL log the HTTP status code and response body and SHALL return an error response indicating checkout creation failed.

### Requirement 10: Admin Upgrade for Additional Members

**User Story:** As a space admin, I want to upgrade my plan to support more members, so that my growing team can use the platform.

#### Acceptance Criteria

1. WHEN an Admin selects a higher-tier variant from the available plan options, THE Billing_Service SHALL create a LemonSqueezy checkout session for the Admin-selected variant ID, including the Space ID in the checkout metadata.
2. IF an Admin requests a plan upgrade while the Space_Subscription status is not "active" and not "trialing", THEN THE Billing_Service SHALL reject the request with an error indicating the subscription must be active or trialing to upgrade.
3. WHEN the Billing_Service receives a subscription_updated webhook with a variant_id that differs from the current Space_Subscription tier ID, THE Billing_Service SHALL update the Space_Subscription tier ID to the new variant_id from the webhook payload.
4. WHEN a member is added to the Space, THE Billing_Service SHALL compare the current member count to the stored peak member count and update the peak member count if the current count is higher.
5. WHEN the Space_Subscription billing period changes, THE Billing_Service SHALL reset the peak member count to zero for the new billing period.
6. IF the LemonSqueezy checkout session creation fails during a plan upgrade, THEN THE Billing_Service SHALL return an error response indicating the upgrade checkout could not be created and leave the Space_Subscription unchanged.
