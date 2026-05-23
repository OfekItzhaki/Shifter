# 483 — ITrialDurationCache Interface

## Phase

Space-Level Billing — Application Layer Interfaces

## Purpose

Defines the contract for accessing the trial duration configured in LemonSqueezy. The interface lives in the Application layer so that commands (e.g. space creation, migration) can retrieve the trial duration without depending on HTTP or infrastructure concerns. The implementation (in Infrastructure) handles caching, periodic sync, and fallback to a 14-day default.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/ITrialDurationCache.cs` | Interface with `GetTrialDaysAsync` and `SyncFromLemonSqueezyAsync` methods |

## Key decisions

- **Two-method design**: `GetTrialDaysAsync` is a fast, synchronous-path read from cache; `SyncFromLemonSqueezyAsync` is a separate method intended for background jobs, keeping the hot path free of network calls.
- **Default parameter `ct = default`**: Matches the existing interface style in the project (`ILemonSqueezyClient`, `IWebhookSignatureValidator`).
- **14-day fallback documented in contract**: The interface XML docs specify the fallback behavior so implementations are consistent.

## How it connects

- **Consumed by**: `CreateSpaceCheckoutCommand`, `MigrateToSpaceBillingCommand`, space creation logic (task 11.1), and `SyncTrialDurationCommand` (task 5.6).
- **Implemented by**: `TrialDurationCache` in `Jobuler.Infrastructure/Billing/` (task 3.4).
- **Sibling interface**: `IStatisticsPeriodService` (task 3.1) — both live in `Jobuler.Application/Billing/`.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application/Jobuler.Application.csproj
```

The file should compile with zero errors or warnings.

## What comes next

- Task 3.4: Implement `TrialDurationCache` in Infrastructure with in-memory cache, 6-hour sync interval, and LemonSqueezy API fetch.
- Task 5.6: `SyncTrialDurationCommand` background job that calls `SyncFromLemonSqueezyAsync`.
- Task 11.1: Space creation logic uses `GetTrialDaysAsync` to determine trial length.

## Git commit

```bash
git add -A && git commit -m "feat(billing): add ITrialDurationCache interface in Application layer"
```
