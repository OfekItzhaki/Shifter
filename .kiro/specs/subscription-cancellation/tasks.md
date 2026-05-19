# Implementation Plan: Subscription Cancellation & Renewal

## Overview

This plan extends the existing billing system with a full subscription lifecycle: cancel → expire → renew. The `GroupSubscription` entity gains an `Expired` status and `Renew()` / `Expire()` methods, the `CancelSubscriptionCommand` is refactored to add audit logging and authorization, new commands are introduced for renewal and batch expiry, and a background job handles automatic expiration. The `Group.Deactivate()` method enforces Limited_Mode for expired subscriptions.

## Tasks

- [x] 1. Domain layer extensions
  - [x] 1.1 Add `Expired` status to `SubscriptionStatus` enum and add `Expire()` / `Renew()` methods to `GroupSubscription`
    - Add `Expired` to `SubscriptionStatus` enum in `Jobuler.Domain/Billing/GroupSubscription.cs`
    - Implement `Expire()` method: only transitions from `Canceled`, throws `InvalidOperationException` otherwise
    - Implement `Renew(DateTime periodStart, DateTime periodEnd)` method: transitions from `Canceled` or `Expired` to `Active`, clears `CanceledAt`, sets period dates; throws if already `Active`
    - Add `Reactivate()` method to `Group` entity (sets `IsActive = true`, calls `Touch()`)
    - _Requirements: 1.1, 2.1, 3.1, 3.2, 3.5_

  - [x] 1.2 Add `BillingManage` permission constant
    - Add `public const string BillingManage = "billing.manage";` to `Permissions` class in `Jobuler.Domain/Spaces/SpacePermissionGrant.cs`
    - _Requirements: 5.1, 5.2_

  - [x]* 1.3 Write property test: Cancel transitions to Canceled with timestamp (Property 1)
    - **Property 1: Cancel transitions subscription to Canceled with timestamp**
    - Generate `GroupSubscription` instances in `Active` status with random period dates; verify `Cancel()` sets `Status == Canceled` and `CanceledAt != null`
    - Use xUnit + FsCheck
    - **Validates: Requirements 1.1**

  - [x]* 1.4 Write property test: Already-canceled subscription rejects cancellation (Property 2)
    - **Property 2: Already-canceled subscription rejects cancellation**
    - Generate `GroupSubscription` in `Canceled` or `Expired` status; verify calling `Cancel()` throws `InvalidOperationException`
    - **Validates: Requirements 1.3**

  - [x]* 1.5 Write property test: Active subscription rejects renewal (Property 11)
    - **Property 11: Active subscription rejects renewal**
    - Generate `GroupSubscription` in `Active` status; verify calling `Renew()` throws `InvalidOperationException`
    - **Validates: Requirements 3.5**

  - [x]* 1.6 Write property test: Renew canceled subscription within period reverts to active (Property 8)
    - **Property 8: Renew canceled subscription within period reverts to active**
    - Generate `GroupSubscription` in `Canceled` status with `CurrentPeriodEnd > DateTime.UtcNow`; verify `Renew()` sets `Status == Active`, `CanceledAt == null`, and preserves existing period dates
    - **Validates: Requirements 3.1**

  - [x]* 1.7 Write property test: Renew expired subscription creates new billing period (Property 9)
    - **Property 9: Renew expired subscription creates new billing period**
    - Generate `GroupSubscription` in `Expired` status; verify `Renew(periodStart, periodEnd)` sets `Status == Active`, `CanceledAt == null`, and updates period dates to provided values
    - **Validates: Requirements 3.2**

- [x] 2. Checkpoint - Ensure domain tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 3. Application layer commands and validators
  - [x] 3.1 Refactor `CancelSubscriptionCommand` with authorization, audit logging, and trialing handling
    - Add `ActorUserId` parameter to the command record
    - Add permission check via `IPermissionService.RequirePermissionAsync` for `BillingManage` (space owners pass implicitly)
    - Add guard: throw `InvalidOperationException` if subscription is already `Canceled` or `Expired`
    - Add trialing logic: if subscription is `Trialing`, cancel and immediately call `group.Deactivate()`
    - Add audit log entry with action `subscription.cancel`, actor_user_id, space_id, group_id, timestamp
    - Remove the immediate `ClosePeriodAsync` call (expiry is now handled by background job)
    - Add `CancelSubscriptionValidator` with FluentValidation (SpaceId, GroupId, ActorUserId not empty)
    - _Requirements: 1.1, 1.3, 1.4, 1.5, 5.1_

  - [x] 3.2 Implement `RenewSubscriptionCommand` handler
    - Create `RenewSubscriptionCommand(Guid SpaceId, Guid GroupId, Guid ActorUserId) : IRequest`
    - Add permission check for `BillingManage`
    - Load subscription; throw `KeyNotFoundException` if not found
    - If `Canceled` and within period: call `subscription.Renew()` preserving existing period
    - If `Expired`: call `subscription.Renew(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1))` to create new period
    - If `Active`: throw `InvalidOperationException`
    - Reactivate group: load group, call `group.Reactivate()` (set `IsActive = true`) if it was deactivated
    - Add audit log entry with action `subscription.renew`
    - Add `RenewSubscriptionValidator` with FluentValidation
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 5.2_

  - [x] 3.3 Implement `ExpireSubscriptionsCommand` handler (batch expiry)
    - Create `ExpireSubscriptionsCommand() : IRequest`
    - Query all subscriptions where `Status == Canceled` and `CurrentPeriodEnd < DateTime.UtcNow`
    - Also query trialing subscriptions where `Status == Trialing` and `TrialEndsAt < DateTime.UtcNow` (already canceled ones)
    - For each: call `subscription.Expire()`, load associated group, call `group.Deactivate()`
    - Add audit log entry with action `subscription.expire` (system actor)
    - Save all changes in a single transaction
    - _Requirements: 2.1, 2.2, 2.5_

  - [x] 3.4 Extend `GetSubscriptionQuery` response with cancellation and expiry fields
    - Add `CanceledAt` (DateTime?) and `PeriodEndsAt` (DateTime?) fields to `SubscriptionDto`
    - Update the query handler to map these fields from the subscription entity
    - _Requirements: 4.1, 4.2, 4.3_

  - [x]* 3.5 Write property test: Trialing subscription cancel causes immediate group deactivation (Property 3)
    - **Property 3: Trialing subscription cancel causes immediate group deactivation**
    - Generate `GroupSubscription` in `Trialing` status; verify canceling sets subscription to `Canceled` and group `IsActive` to `false`
    - **Validates: Requirements 1.4**

  - [x]* 3.6 Write property test: Expired subscription deactivates group (Property 5)
    - **Property 5: Expired subscription deactivates group**
    - Generate `GroupSubscription` in `Canceled` status with `CurrentPeriodEnd <= DateTime.UtcNow`; verify expiry logic transitions to `Expired` and sets group `IsActive = false`
    - **Validates: Requirements 2.1, 2.2**

  - [x]* 3.7 Write property test: Active subscriptions are not expired by the expiry job (Property 6)
    - **Property 6: Active subscriptions are not expired by the expiry job**
    - Generate `GroupSubscription` in `Active` status with various `CurrentPeriodEnd` values; verify the expiry job does NOT change status and group remains active
    - **Validates: Requirements 2.5**

  - [x]* 3.8 Write property test: Renewal reactivates group from Limited_Mode (Property 10)
    - **Property 10: Renewal reactivates group from Limited_Mode**
    - Generate a group with `IsActive == false` and associated expired/canceled subscription; verify renewal sets `group.IsActive = true`
    - **Validates: Requirements 3.3**

  - [x]* 3.9 Write property test: Unauthorized users are rejected (Property 13)
    - **Property 13: Unauthorized users are rejected for cancel and renew**
    - Generate random user IDs without `SpaceOwner` role or `BillingManage` permission; verify both cancel and renew throw `UnauthorizedAccessException`
    - **Validates: Requirements 5.1, 5.2, 5.3**

- [x] 4. Checkpoint - Ensure application layer tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. API layer and background job
  - [x] 5.1 Add cancel and renew endpoints to `BillingController`
    - Add `POST /spaces/{spaceId}/billing/groups/{groupId}/cancel` endpoint
    - Add `POST /spaces/{spaceId}/billing/groups/{groupId}/renew` endpoint
    - Both endpoints extract `CurrentUserId` from claims, dispatch respective commands via MediatR
    - Permission checks happen in command handlers (per architecture rules)
    - _Requirements: 1.1, 3.1, 5.1, 5.2, 5.3_

  - [x] 5.2 Implement `ExpireSubscriptionsJob` as a recurring hosted service
    - Create `ExpireSubscriptionsJob` in `Jobuler.Infrastructure` (or as a Hangfire recurring job if Hangfire is configured)
    - Schedule to run daily (or every few hours)
    - Dispatches `ExpireSubscriptionsCommand` via MediatR
    - Add logging for number of subscriptions expired per run
    - _Requirements: 2.1, 2.2, 2.5_

  - [x] 5.3 Extend `GetSubscription` endpoint response mapping
    - Update the existing `GET /spaces/{spaceId}/billing/groups/{groupId}/subscription` endpoint to return the extended `SubscriptionDto` with `canceledAt` and `periodEndsAt`
    - _Requirements: 4.1, 4.2, 4.3_

  - [x]* 5.4 Write property test: Status query returns correct fields per subscription state (Property 12)
    - **Property 12: Status query returns correct fields per subscription state**
    - Generate `GroupSubscription` in various states; verify the DTO includes `canceledAt` (non-null) when `Canceled`, and `status == "expired"` when `Expired`
    - **Validates: Requirements 4.1, 4.2**

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Limited_Mode enforcement and integration wiring
  - [x] 7.1 Add Limited_Mode guards to write operations
    - Identify controllers/commands that create schedules, assignments, and solver runs
    - Add a check: if `group.IsActive == false`, throw `InvalidOperationException` with message indicating the group is in limited mode
    - This can be a shared guard method or middleware-level check on group-scoped write endpoints
    - _Requirements: 2.3, 2.4_

  - [x]* 7.2 Write property test: Limited_Mode blocks write operations (Property 7)
    - **Property 7: Limited_Mode blocks write operations**
    - Generate groups with `IsActive == false`; verify attempts to create schedules, assignments, or solver runs are rejected
    - **Validates: Requirements 2.4**

  - [x]* 7.3 Write integration tests for full lifecycle
    - Test cancel → expire → renew lifecycle via command handlers
    - Verify read-only access in Limited_Mode (read queries succeed, write commands throw)
    - Verify audit log entries are created at each step
    - _Requirements: 1.1, 1.5, 2.1, 2.3, 2.4, 3.1, 3.3, 3.4_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- Backend uses C# with .NET (MediatR, FluentValidation, EF Core, xUnit, FsCheck)
- The existing `CancelSubscriptionCommand` at `Jobuler.Application/Billing/Commands/CancelSubscriptionCommand.cs` will be refactored (not replaced)
- The existing `GroupSubscription` entity at `Jobuler.Domain/Billing/GroupSubscription.cs` already has a `Cancel()` method and `CanceledAt` field
- The existing `Group` entity at `Jobuler.Domain/Groups/Group.cs` already has `Deactivate()` but needs a `Reactivate()` method
- The `Permissions` class at `Jobuler.Domain/Spaces/SpacePermissionGrant.cs` needs the new `BillingManage` constant
- The `GetSubscriptionQuery` at `Jobuler.Application/Billing/Queries/GetSubscriptionQuery.cs` needs extended DTO fields
- Space owners implicitly hold all permissions per existing `PermissionService` logic — no special handling needed

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "1.4", "1.5", "1.6", "1.7"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4"] },
    { "id": 3, "tasks": ["3.5", "3.6", "3.7", "3.8", "3.9"] },
    { "id": 4, "tasks": ["5.1", "5.2", "5.3"] },
    { "id": 5, "tasks": ["5.4", "7.1"] },
    { "id": 6, "tasks": ["7.2", "7.3"] }
  ]
}
```
