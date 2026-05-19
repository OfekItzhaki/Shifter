# Requirements Document

## Introduction

This feature adds a subscription cancellation and renewal workflow to Shifter. Space owners can cancel their group subscription, which continues to function until the current billing period ends. After the period expires without renewal, the space/group becomes limited. Owners can renew at any time (before or after expiry) to start a new billing period and restore full access.

## Glossary

- **Subscription_Service**: The backend service responsible for managing subscription lifecycle operations (cancel, renew, expire)
- **Space_Owner**: The user who owns a space and has authority to manage its billing
- **Billing_Period**: A time range (CurrentPeriodStart to CurrentPeriodEnd) during which the subscription is paid and active
- **Cancellation_Grace_Window**: The remaining time between cancellation request and the end of the current billing period, during which the subscription remains fully functional
- **Limited_Mode**: A restricted state applied to a group/space after subscription expiry, where scheduling and management features are read-only
- **Renewal**: The act of reactivating a canceled or expired subscription, which starts a new billing period

## Requirements

### Requirement 1: Cancel Subscription

**User Story:** As a space owner, I want to cancel my group's subscription, so that I am not charged for the next billing period.

#### Acceptance Criteria

1. WHEN the Space_Owner submits a cancellation request for a group, THE Subscription_Service SHALL mark the subscription status as "canceled" and record the cancellation timestamp
2. WHILE the subscription is in the Cancellation_Grace_Window, THE Subscription_Service SHALL maintain full access to all features for the group
3. IF the subscription is already canceled, THEN THE Subscription_Service SHALL return an error indicating the subscription is already canceled
4. IF the subscription is in "trialing" status, THEN THE Subscription_Service SHALL cancel the subscription immediately and transition the group to Limited_Mode
5. WHEN a cancellation request is submitted, THE Subscription_Service SHALL produce an audit log entry containing actor_user_id, space_id, group_id, action "subscription.cancel", and timestamp

### Requirement 2: Expire Subscription After Period End

**User Story:** As a system operator, I want canceled subscriptions to expire automatically after the billing period ends, so that unpaid groups transition to limited functionality.

#### Acceptance Criteria

1. WHEN the current Billing_Period ends for a canceled subscription, THE Subscription_Service SHALL transition the group to Limited_Mode
2. WHEN a group enters Limited_Mode, THE Subscription_Service SHALL set the group's IsActive flag to false
3. WHILE a group is in Limited_Mode, THE Subscription_Service SHALL allow read-only access to existing schedules and data
4. WHILE a group is in Limited_Mode, THE Subscription_Service SHALL prevent creation of new schedules, assignments, and solver runs
5. IF the subscription is active (not canceled) when the Billing_Period ends, THEN THE Subscription_Service SHALL renew the period automatically without entering Limited_Mode

### Requirement 3: Renew Subscription

**User Story:** As a space owner, I want to renew my canceled or expired subscription, so that my group regains full access with a new billing period.

#### Acceptance Criteria

1. WHEN the Space_Owner submits a renewal request for a canceled subscription (still within Billing_Period), THE Subscription_Service SHALL revert the status to "active" and clear the cancellation timestamp
2. WHEN the Space_Owner submits a renewal request for an expired subscription (past Billing_Period), THE Subscription_Service SHALL create a new Billing_Period starting from the current date and set the status to "active"
3. WHEN a group is renewed from Limited_Mode, THE Subscription_Service SHALL restore the group's IsActive flag to true and remove all Limited_Mode restrictions
4. WHEN a renewal is processed, THE Subscription_Service SHALL produce an audit log entry containing actor_user_id, space_id, group_id, action "subscription.renew", and timestamp
5. IF the subscription is already active and not canceled, THEN THE Subscription_Service SHALL return an error indicating the subscription does not need renewal

### Requirement 4: Subscription Status Visibility

**User Story:** As a space owner, I want to see the current subscription status including cancellation and expiry details, so that I can make informed billing decisions.

#### Acceptance Criteria

1. WHEN the Space_Owner queries subscription status for a canceled subscription, THE Subscription_Service SHALL return the status, cancellation date, and the date when the Billing_Period ends
2. WHEN the Space_Owner queries subscription status for an expired subscription, THE Subscription_Service SHALL return the status as "expired" and the date when Limited_Mode began
3. THE Subscription_Service SHALL include a "canceledAt" field and a "periodEndsAt" field in the subscription status response

### Requirement 5: Authorization for Cancellation and Renewal

**User Story:** As a system administrator, I want only authorized space owners to cancel or renew subscriptions, so that billing changes are protected from unauthorized access.

#### Acceptance Criteria

1. THE Subscription_Service SHALL require the caller to hold SpaceOwner or BillingManage permission before processing a cancellation request
2. THE Subscription_Service SHALL require the caller to hold SpaceOwner or BillingManage permission before processing a renewal request
3. IF the caller does not hold the required permission, THEN THE Subscription_Service SHALL return a 403 Forbidden response
