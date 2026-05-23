# 483 — IStatisticsPeriodService Interface

## Phase

Space-Level Billing — Application Layer Interfaces

## Purpose

Defines the contract for managing statistics period boundaries in response to subscription lifecycle events. Each method corresponds to a lifecycle transition (trial start, trial expiry, subscription activation, subscription expiry, period renewal) and is responsible for closing active `SubscriptionPeriod` records and opening new ones for all groups in the space.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/IStatisticsPeriodService.cs` | Interface with five async methods mapping to subscription lifecycle events |

## Key decisions

- Methods accept explicit boundary dates rather than computing them internally — keeps the service stateless and testable.
- `CancellationToken` is a required parameter (not defaulted) to match the command handler calling pattern where cancellation is always propagated.
- XML doc comments follow the same style as `ILemonSqueezyClient` and `IWebhookSignatureValidator` in the same folder.
- Interface lives in `Jobuler.Application/Billing/` so it can be consumed by command handlers without depending on Infrastructure.

## How it connects

- **Consumed by**: Space subscription command handlers (`HandleSpaceSubscriptionCreatedCommand`, `CancelSpaceSubscriptionCommand`, `ExpireSpaceSubscriptionsCommand`, `RenewSpaceSubscriptionCommand`, space creation flow).
- **Implemented by**: `StatisticsPeriodService` in `Jobuler.Infrastructure/Billing/` (task 3.3).
- **Operates on**: `SubscriptionPeriod` entities in `Jobuler.Domain/Scheduling/`.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj
```

The interface has no implementation yet, so no runtime verification is needed — just confirm it compiles without errors.

## What comes next

- Task 3.2: `ITrialDurationCache` interface (same folder)
- Task 3.3: `StatisticsPeriodService` implementation in Infrastructure
- Task 3.5: Property test for statistics period rotation (Property 13)

## Git commit

```bash
git add -A && git commit -m "feat(billing): add IStatisticsPeriodService interface for period boundaries"
```
