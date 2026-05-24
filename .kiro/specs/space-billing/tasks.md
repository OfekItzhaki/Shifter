# Implementation Plan: Space-Level Billing

## Overview

This plan migrates billing from per-group subscriptions to per-space subscriptions. Implementation follows the Clean Architecture layers (Domain → Application → Infrastructure → Api → Frontend), starting with the domain entity, then wiring through commands/queries, adding webhook handling, statistics period boundaries, migration logic, and finally frontend updates.

## Tasks

- [ ] 1. Domain layer — SpaceSubscription entity and enum updates
  - [x] 1.1 Create `SpaceSubscription` entity in `Jobuler.Domain/Billing/SpaceSubscription.cs`
    - Add all properties: SpaceId, TierId, Status, LemonSqueezySubscriptionId, LemonSqueezyCustomerId, TrialStartsAt, TrialEndsAt, CurrentPeriodStart, CurrentPeriodEnd, PeakMemberCount, CanceledAt, AutoRenew
    - Implement `CreateTrial(Guid spaceId, int trialDays)` factory method
    - Implement state transitions: `Activate()`, `Cancel()`, `Expire()`, `RenewWithinGracePeriod()`, `RenewAfterExpiry()`, `UpdatePeriod()`, `UpdateTier()`, `UpdatePeakMemberCount()`, `ResetPeakForNewPeriod()`, `SetAutoRenew()`
    - Implement computed properties: `IsAccessGranted`, `IsTrialExpired`, `DaysRemaining`
    - Add guard clauses for invalid state transitions (throw `InvalidOperationException`)
    - _Requirements: 1.1, 1.3, 1.4, 1.6, 6.1, 6.2, 6.5, 6.6, 6.7, 10.4, 10.5_

  - [x] 1.2 Add `Migrated` value to `SubscriptionStatus` enum in `GroupSubscription.cs`
    - Add `Migrated` to the existing `SubscriptionStatus` enum
    - _Requirements: 8.1_

  - [x]* 1.3 Write property tests for SpaceSubscription entity (Properties 1, 9, 12)
    - **Property 1: Trial date computation** — For any positive trial duration (1–365), `CreateTrial` produces correct `TrialStartsAt` and `TrialEndsAt`
    - **Property 9: Cancel state transition** — Active/Trialing → Cancel succeeds; Canceled/Expired → Cancel throws
    - **Property 12: Renewal preserves or resets period** — Within grace period preserves dates; after expiry sets new dates; active throws
    - **Validates: Requirements 1.3, 1.4, 6.1, 6.2, 6.5, 6.6, 6.7**

  - [x]* 1.4 Write property tests for access and expiry logic (Properties 3, 4, 10, 11)
    - **Property 3: Access granted when active or trialing** — IsAccessGranted returns true for Active/Trialing (non-expired)
    - **Property 4: Access denied when inactive** — IsAccessGranted returns false for Expired/PastDue/Canceled-past-period
    - **Property 10: Grace period access** — Canceled with CurrentPeriodEnd > now still grants access
    - **Property 11: Expiry state transition** — Canceled with CurrentPeriodEnd <= now can be expired
    - **Validates: Requirements 2.1, 2.2, 2.3, 6.3, 6.4**

  - [x]* 1.5 Write property tests for peak member count and upgrade guard (Properties 15, 16)
    - **Property 15: Peak member count tracking** — PeakMemberCount always equals max observed; resets on period change
    - **Property 16: Upgrade guard** — Upgrade rejected when status is not Active/Trialing
    - **Validates: Requirements 10.2, 10.4, 10.5**

- [ ] 2. Infrastructure layer — Persistence and EF configuration
  - [x] 2.1 Create EF configuration `SpaceSubscriptionConfiguration` in `Jobuler.Infrastructure/Persistence/Configurations/`
    - Map to `space_subscriptions` table with snake_case column names
    - Configure unique index on `space_id`, index on `status`
    - Configure `Status` as string conversion
    - Register `DbSet<SpaceSubscription>` in `AppDbContext`
    - _Requirements: 1.1, 1.6_

  - [x] 2.2 Create EF migration for `space_subscriptions` table
    - Generate migration via `dotnet ef migrations add AddSpaceSubscriptions`
    - Verify migration includes all columns, constraints, and indexes from design
    - _Requirements: 1.1_

  - [x] 2.3 Update `GroupSubscription` EF configuration to support `Migrated` status value
    - Ensure the string conversion handles the new `Migrated` enum value
    - _Requirements: 8.1_

- [ ] 3. Application layer — Interfaces and services
  - [x] 3.1 Create `IStatisticsPeriodService` interface in `Jobuler.Application/Billing/`
    - Define methods: `OnTrialStartedAsync`, `OnTrialExpiredAsync`, `OnSubscriptionActivatedAsync`, `OnSubscriptionExpiredAsync`, `OnPeriodRenewedAsync`
    - Each method accepts `spaceId`, boundary date, and `CancellationToken`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 3.2 Create `ITrialDurationCache` interface in `Jobuler.Application/Billing/`
    - Define `GetTrialDaysAsync(CancellationToken)` returning `Task<int>`
    - Define `SyncFromLemonSqueezyAsync(CancellationToken)` for background sync
    - _Requirements: 1.2, 1.5, 1.7_

  - [x] 3.3 Implement `StatisticsPeriodService` in `Jobuler.Infrastructure/Billing/`
    - Close all active `SubscriptionPeriod` records for groups in the space
    - Open new `SubscriptionPeriod` records for each group with the boundary date
    - Skip if no groups exist in the space (log for reconciliation)
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6_

  - [x] 3.4 Implement `TrialDurationCache` in `Jobuler.Infrastructure/Billing/`
    - In-memory cache with 6-hour sync interval
    - Fallback to 14 days when cache is unavailable
    - Fetch trial_duration_days from LemonSqueezy variant attributes on sync
    - _Requirements: 1.2, 1.5, 1.7_

  - [x]* 3.5 Write property test for statistics period rotation (Property 13)
    - **Property 13: Lifecycle events rotate statistics periods** — For N groups, lifecycle event closes N active periods and opens N new periods
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5**

- [x] 4. Checkpoint — Domain and infrastructure foundation
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Application layer — Space subscription commands
  - [x] 5.1 Create `CreateSpaceCheckoutCommand` and handler
    - Verify `BillingManage` permission
    - Reject if subscription status is already `Active` (throw `InvalidOperationException`)
    - Call `ILemonSqueezyClient.CreateCheckoutAsync` with `space_id` in metadata
    - Add `CreateSpaceCheckoutValidator` with FluentValidation
    - _Requirements: 5.1, 5.2_

  - [x] 5.2 Create `CancelSpaceSubscriptionCommand` and handler
    - Verify `BillingManage` permission
    - Load SpaceSubscription, call `Cancel()`
    - Trigger `IStatisticsPeriodService` if transitioning from trialing
    - Add `CancelSpaceSubscriptionValidator`
    - _Requirements: 6.1, 6.2_

  - [x] 5.3 Create `RenewSpaceSubscriptionCommand` and handler
    - Verify `BillingManage` permission
    - Determine if within grace period or expired, call appropriate renew method
    - Trigger `IStatisticsPeriodService.OnPeriodRenewedAsync` for expired renewals
    - Add `RenewSpaceSubscriptionValidator`
    - _Requirements: 6.5, 6.6, 6.7_

  - [x] 5.4 Create `UpgradeSpacePlanCommand` and handler
    - Verify `BillingManage` permission
    - Reject if status is not Active/Trialing (throw `InvalidOperationException`)
    - Create LemonSqueezy checkout with selected variant ID and `space_id` metadata
    - Add `UpgradeSpacePlanValidator`
    - _Requirements: 10.1, 10.2, 10.6_

  - [x] 5.5 Create `ExpireSpaceSubscriptionsCommand` and handler (background job)
    - Query all SpaceSubscriptions with status `Canceled` and `CurrentPeriodEnd <= now`
    - Call `Expire()` on each, trigger `IStatisticsPeriodService.OnSubscriptionExpiredAsync`
    - _Requirements: 6.4_

  - [x] 5.6 Create `SyncTrialDurationCommand` and handler (background job)
    - Call `ITrialDurationCache.SyncFromLemonSqueezyAsync`
    - _Requirements: 1.7_

  - [x]* 5.7 Write property tests for checkout and upgrade commands (Properties 6, 7, 16)
    - **Property 6: Checkout metadata always includes space_id** — All checkout calls include space_id in metadata
    - **Property 7: Active subscription rejects checkout** — Active status throws, does not call LemonSqueezy
    - **Property 16: Upgrade guard** — Non-active/non-trialing status rejects upgrade
    - **Validates: Requirements 5.1, 5.2, 9.4, 10.2**

- [ ] 6. Application layer — Webhook handling commands
  - [x] 6.1 Create `HandleSpaceSubscriptionCreatedCommand` and handler
    - Load SpaceSubscription by `space_id` from metadata
    - Call `Activate()` with tier, LS subscription ID, customer ID, period dates from payload
    - Trigger `IStatisticsPeriodService.OnSubscriptionActivatedAsync`
    - _Requirements: 5.3_

  - [x] 6.2 Create `HandleSpaceSubscriptionUpdatedCommand` and handler
    - Load SpaceSubscription by `space_id` from metadata
    - Update period dates via `UpdatePeriod()`
    - If variant_id differs from current TierId, call `UpdateTier()`
    - Trigger `IStatisticsPeriodService.OnPeriodRenewedAsync` if period changed
    - _Requirements: 6.8, 10.3_

  - [x] 6.3 Create `HandleSpaceSubscriptionCancelledCommand` and handler
    - Load SpaceSubscription by `space_id` from metadata
    - Call `Cancel()` on the entity
    - _Requirements: 6.1_

  - [x] 6.4 Update `HandleWebhookCommand` handler to route space-level events
    - Check if metadata contains `space_id` key
    - If present, dispatch to space-level handlers (Created/Updated/Cancelled)
    - If absent, fall through to existing group-level handlers (backward compatibility)
    - Maintain idempotency check via `WebhookEventLog`
    - _Requirements: 5.3, 5.4, 8.6_

  - [x]* 6.5 Write property tests for webhook handling (Properties 2, 8)
    - **Property 2: Subscription creation idempotency** — Duplicate creation does not create second subscription
    - **Property 8: Webhook idempotency** — Duplicate event ID does not modify state
    - **Validates: Requirements 1.6, 5.3**

- [ ] 7. Application layer — Queries
  - [x] 7.1 Create `GetSpaceSubscriptionQuery` and handler
    - Return `SpaceSubscriptionDto` with status, dates, tier, daysRemaining, isActive, autoRenew
    - Require `SpaceView` permission
    - _Requirements: 3.1, 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 7.2 Create `GetSpaceBillingAccessQuery` and handler
    - Accept SpaceId and GroupId
    - Load SpaceSubscription for the space, return `IsAccessGranted`
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

- [x] 8. Checkpoint — Application layer complete
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 9. Application layer — Migration command
  - [x] 9.1 Create `MigrateToSpaceBillingCommand` and handler
    - Accept `BatchSize` parameter
    - For each space in batch: mark all GroupSubscriptions as `Migrated`
    - If space has active/trialing GroupSubscription: create SpaceSubscription with `Active` status and latest period dates
    - If space has no active/trialing GroupSubscription: create SpaceSubscription with `Trialing` status
    - Skip spaces that already have a SpaceSubscription
    - Wrap each batch in a transaction; rollback on failure within batch
    - Log progress and errors
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x]* 9.2 Write property test for migration correctness (Property 14)
    - **Property 14: Migration creates correct space subscriptions** — All group subs marked migrated; active spaces get Active SpaceSubscription; inactive get Trialing; existing SpaceSubscriptions skipped
    - **Validates: Requirements 8.1, 8.2, 8.3, 8.4**

- [ ] 10. API layer — Space billing endpoints
  - [x] 10.1 Add space-level billing endpoints to `BillingController`
    - `GET /spaces/{spaceId}/billing/subscription` → dispatches `GetSpaceSubscriptionQuery`
    - `POST /spaces/{spaceId}/billing/checkout` → dispatches `CreateSpaceCheckoutCommand`
    - `POST /spaces/{spaceId}/billing/cancel` → dispatches `CancelSpaceSubscriptionCommand`
    - `POST /spaces/{spaceId}/billing/renew` → dispatches `RenewSpaceSubscriptionCommand`
    - `POST /spaces/{spaceId}/billing/upgrade` → dispatches `UpgradeSpacePlanCommand` (accept `variantId` in body)
    - All endpoints require `[Authorize]` and permission checks
    - _Requirements: 4.6, 5.1, 6.1, 6.5, 10.1_

  - [x] 10.2 Add migration endpoint and post-migration group billing rejection
    - Add `POST /admin/billing/migrate` endpoint (admin-only) → dispatches `MigrateToSpaceBillingCommand`
    - Update existing group-level billing endpoints to return 410 Gone when space has a SpaceSubscription
    - _Requirements: 8.6_

  - [x]* 10.3 Write property test for webhook signature rejection (Property 17)
    - **Property 17: Webhook signature rejection** — Invalid HMAC returns 401, no state modification
    - **Validates: Requirements 5.4**

- [ ] 11. Infrastructure — Wire space subscription creation into space creation flow
  - [x] 11.1 Update space creation logic to auto-create SpaceSubscription
    - After a Space is created, create a `SpaceSubscription.CreateTrial(spaceId, trialDays)` using `ITrialDurationCache`
    - Trigger `IStatisticsPeriodService.OnTrialStartedAsync`
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 7.1_

  - [x] 11.2 Update member addition logic to track peak member count
    - When a member is added to a space, load SpaceSubscription and call `UpdatePeakMemberCount(currentCount)`
    - _Requirements: 10.4_

- [x] 12. Checkpoint — Backend complete
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 13. Frontend — API client and TrialBanner update
  - [x] 13.1 Add space billing API client functions
    - Add `getSpaceSubscription(spaceId)` to billing API module
    - Add `createSpaceCheckout(spaceId, variantId?)` to billing API module
    - Add `cancelSpaceSubscription(spaceId)` to billing API module
    - Add `renewSpaceSubscription(spaceId)` to billing API module
    - Add `upgradeSpacePlan(spaceId, variantId)` to billing API module
    - Define `SpaceSubscriptionDto` TypeScript interface
    - _Requirements: 5.1, 6.1, 6.5, 10.1_

  - [x] 13.2 Update `TrialBanner` component for space-level subscription
    - Remove `groupId` prop dependency
    - Fetch from `GET /spaces/{spaceId}/billing/subscription` instead of group endpoint
    - Display days remaining with color logic: sky >7d, amber 4-7d, red ≤3d
    - Show upgrade prompt when days remaining is 0
    - Show subscription expiry warning for non-auto-renewing active subscriptions within 7 days
    - Hide banner when active + auto-renewing
    - Hide banner on API failure (fail silent)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6_

  - [x]* 13.3 Write property test for days remaining and color computation (Property 5)
    - **Property 5: Days remaining and color computation** — daysRemaining = ceil((trialEndsAt - now) / 1 day); color follows sky/amber/red thresholds
    - **Validates: Requirements 3.1, 3.4, 3.6**

- [ ] 14. Frontend — SpaceBillingCard component
  - [x] 14.1 Create `SpaceBillingCard` component on space settings page
    - Display subscription status badge (trialing/active/past_due/canceled/expired)
    - Show trial start/end dates when trialing (YYYY-MM-DD format)
    - Show period start/end dates when active
    - Show cancellation date and access expiry when canceled
    - Show "no subscription" message when none exists
    - Permission-gate: only visible to users with `BillingManage`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [x] 14.2 Add action buttons to `SpaceBillingCard`
    - Upgrade button (visible when trialing or active)
    - Cancel button (visible when active or trialing)
    - Renew button (visible when canceled or expired)
    - Handle loading states and error toasts
    - On checkout success, redirect to LemonSqueezy checkout URL
    - _Requirements: 5.1, 6.1, 6.5, 10.1_

  - [x]* 14.3 Write unit tests for SpaceBillingCard
    - Test correct date display per status
    - Test permission gating hides section for non-admins
    - Test error state shows retry button
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

- [x] 15. Final checkpoint — Full integration
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (FsCheck, minimum 100 iterations)
- Unit tests validate specific examples and edge cases
- The existing group-level billing endpoints remain functional until migration completes, then return 410 Gone
- Step documentation under `docs/steps/` should be created alongside each implementation task per workspace conventions
- All commands require FluentValidation validators per architecture rules
- All endpoints require `[Authorize]` and permission checks per security rules

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4", "1.5", "2.1", "2.3", "3.1", "3.2"] },
    { "id": 2, "tasks": ["2.2", "3.3", "3.4"] },
    { "id": 3, "tasks": ["3.5", "5.1", "5.2", "5.3", "5.4", "5.5", "5.6", "7.1", "7.2"] },
    { "id": 4, "tasks": ["5.7", "6.1", "6.2", "6.3", "6.4"] },
    { "id": 5, "tasks": ["6.5", "9.1"] },
    { "id": 6, "tasks": ["9.2", "10.1", "10.2", "11.1", "11.2"] },
    { "id": 7, "tasks": ["10.3", "13.1"] },
    { "id": 8, "tasks": ["13.2", "13.3", "14.1"] },
    { "id": 9, "tasks": ["14.2", "14.3"] }
  ]
}
```
