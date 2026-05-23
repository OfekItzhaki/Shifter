# 492 — Space Creation Auto-Creates Trial Subscription

## Phase

Space-Level Billing — Infrastructure wiring

## Purpose

When a new space is created, it must automatically receive a trial subscription so that the space owner can start using premium features immediately. This step wires the `CreateSpaceCommandHandler` to create a `SpaceSubscription` in trial status after the space is persisted, using the cached trial duration from LemonSqueezy (with a 14-day fallback). It also triggers the statistics period service to open initial periods for the space's groups.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Spaces/Commands/CreateSpaceCommandHandler.cs` | Updated to inject `ITrialDurationCache` and `IStatisticsPeriodService`. After saving the space, creates a trial subscription (idempotent — skips if one already exists) and triggers `OnTrialStartedAsync`. Also added `BillingManage` to the owner's auto-granted permissions. |
| `apps/api/Jobuler.Tests/Application/CreateSpaceCommandTests.cs` | Added 4 new tests: trial subscription creation, `OnTrialStartedAsync` trigger, trial days from cache, and `BillingManage` permission grant. Updated existing tests for new constructor. |
| `apps/api/Jobuler.Tests/Integration/UserRoleAssignmentFlowTests.cs` | Updated to use mocked `ITrialDurationCache`, `IStatisticsPeriodService`, and `IPeakMemberTracker` for the new constructor signatures. |
| `apps/api/Jobuler.Tests/InvitationFlow/BugConditionExplorationTests.cs` | Fixed pre-existing compilation issue by adding `IPeakMemberTracker` mock to `AddPersonByEmail/PhoneCommandHandler` constructors. |

## Key decisions

1. **Idempotency**: Before creating a subscription, we check `AnyAsync` on `SpaceSubscriptions` for the space ID. This satisfies Requirement 1.6 — no duplicate subscriptions.
2. **Two SaveChanges calls**: The space is saved first (so the FK constraint is satisfied), then the subscription is saved separately. This ensures the space_id FK exists before the subscription row is inserted.
3. **BillingManage permission**: Added to the owner's auto-granted permissions so the space creator can manage billing immediately.
4. **Statistics period trigger**: `OnTrialStartedAsync` is called after the subscription is persisted, using the subscription's `TrialStartsAt` as the boundary date.

## How it connects

- Depends on: `SpaceSubscription` entity (task 1.1), `ITrialDurationCache` (task 3.2/3.4), `IStatisticsPeriodService` (task 3.1/3.3), EF configuration (task 2.1)
- Used by: Any code that creates a space (SpacesController, tests, migration scripts)
- The trial subscription created here is later managed by checkout/cancel/renew/expire commands (tasks 5.x, 6.x)

## How to run / verify

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~CreateSpaceCommandTests"
dotnet test --filter "FullyQualifiedName~UserRoleAssignmentFlowTests"
```

All 9 tests should pass (6 + 3).

## What comes next

- Task 11.2: Wire peak member count tracking into member addition logic
- Frontend tasks: TrialBanner and SpaceBillingCard will consume the subscription created here

## Git commit

```bash
git add -A && git commit -m "feat(billing): auto-create trial subscription on space creation"
```
