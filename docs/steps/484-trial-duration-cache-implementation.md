# 484 — Trial Duration Cache Implementation

## Phase

Space-Level Billing — Infrastructure Layer

## Purpose

Implements the `TrialDurationCache` service that provides cached access to the trial duration configured in the LemonSqueezy product variant. This cache ensures that trial creation doesn't require a synchronous API call to LemonSqueezy on every space creation, while still keeping the value up-to-date via periodic background sync.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Billing/TrialDurationCache.cs` | In-memory cache implementation of `ITrialDurationCache`. Uses `IHttpClientFactory` for HTTP calls, syncs every 6 hours, falls back to 14 days when unavailable. |
| `apps/api/Jobuler.Api/Program.cs` | Registered `TrialDurationCache` as a singleton with a named `HttpClient` ("TrialDurationCache") configured with LemonSqueezy base URL, 10s timeout, and auth headers. |
| `apps/api/Jobuler.Tests/Billing/TrialDurationCacheTests.cs` | Unit tests covering: default fallback, successful sync, HTTP failure resilience, exception resilience, missing variant ID, and missing attribute handling. |

## Key decisions

1. **Singleton with IHttpClientFactory** — The cache holds in-memory state (`_cachedDays`, `_lastSync`) so it must be a singleton. Using `IHttpClientFactory` with a named client avoids the socket exhaustion problem that comes with long-lived `HttpClient` instances in singletons.

2. **6-hour sync interval** — The `GetTrialDaysAsync` method checks if the cache is stale (older than 6 hours). If stale, it still returns the last known value (or 14-day default) rather than blocking on a sync. The actual sync is triggered externally by a background job (`SyncTrialDurationCommand`).

3. **Graceful degradation** — On any failure (HTTP error, network exception, malformed JSON, missing attribute), the cache logs a warning and preserves the existing cached value. This ensures trial creation never fails due to LemonSqueezy being unavailable.

4. **JSON:API response parsing** — Extracts `data.attributes.trial_duration_days` from the LemonSqueezy variant endpoint response. Handles both numeric and string representations of the value.

## How it connects

- **Consumed by**: `CreateSpaceCheckoutCommand` (task 5.1), space creation flow (task 11.1), and `MigrateToSpaceBillingCommand` (task 9.1) — all use `ITrialDurationCache.GetTrialDaysAsync()` to determine trial duration.
- **Synced by**: `SyncTrialDurationCommand` (task 5.6) — a background job that calls `SyncFromLemonSqueezyAsync()` periodically.
- **Interface defined in**: `Jobuler.Application/Billing/ITrialDurationCache.cs` (task 3.2, already complete).
- **Depends on**: `LemonSqueezySettings` for the `DefaultVariantId` and `ApiKey` configuration.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~TrialDurationCacheTests"
```

All 8 tests should pass:
- `GetTrialDaysAsync_WhenNeverSynced_ReturnsFourteenDays`
- `GetTrialDaysAsync_WhenCachePopulated_ReturnsCachedValue`
- `SyncFromLemonSqueezyAsync_ExtractsTrialDurationDays`
- `SyncFromLemonSqueezyAsync_OnHttpFailure_KeepsExistingValue`
- `SyncFromLemonSqueezyAsync_OnException_KeepsExistingValue`
- `SyncFromLemonSqueezyAsync_WhenNeverSyncedAndFails_FallsBackToDefault`
- `SyncFromLemonSqueezyAsync_WhenVariantIdNotConfigured_SkipsSync`
- `SyncFromLemonSqueezyAsync_WhenResponseMissingTrialDuration_KeepsExistingValue`

## What comes next

- Task 3.5: Property test for statistics period rotation
- Task 5.6: `SyncTrialDurationCommand` background job that calls `SyncFromLemonSqueezyAsync` on a schedule
- Task 11.1: Wire `ITrialDurationCache` into the space creation flow to set trial duration

## Git commit

```bash
git add -A && git commit -m "feat(space-billing): implement TrialDurationCache with 6h sync and 14-day fallback"
```
