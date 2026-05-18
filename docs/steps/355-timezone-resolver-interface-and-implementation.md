# 355 — ITimezoneResolver Interface and TimezoneResolver Implementation

## Phase

User Timezone Settings — Backend Domain & Infrastructure (Task 1.2)

## Purpose

Provides a timezone resolution service that maps a user's Country/State geographic selection to an IANA timezone identifier and computes the current UTC offset in minutes. This is the core component that enables timezone-aware time display throughout the application.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Common/ITimezoneResolver.cs` | Interface definition with `Resolve(string? countryCode, string? stateCode)` method and `TimezoneResolution` record |
| `apps/api/Jobuler.Infrastructure/Timezone/TimezoneResolver.cs` | Implementation using static country-timezone mapping with fallback chain |
| `apps/api/Jobuler.Infrastructure/Timezone/CountryTimezoneMap.cs` | Static dictionary mapping country/state codes to IANA timezone IDs (structural skeleton + representative data; full data populated by task 1.3) |
| `apps/api/Jobuler.Api/Program.cs` | DI registration of `ITimezoneResolver` as singleton |

## Key decisions

1. **Singleton lifetime** — The resolver is stateless (uses only static data and `TimeZoneInfo`), so it's registered as a singleton for performance.
2. **Fallback chain** — State → Country (most populous TZ) → `Asia/Jerusalem`. The resolver never throws; it always returns a valid timezone.
3. **`TimeZoneInfo` over NodaTime** — .NET 8 supports IANA IDs natively on all platforms via `TimeZoneInfo.FindSystemTimeZoneById`. No additional dependency needed.
4. **Case-insensitive lookups** — Both dictionaries use `StringComparer.OrdinalIgnoreCase` to handle mixed-case input gracefully.
5. **State key format** — `"CC-SS"` (e.g., `"US-CA"`) for state-level lookups, matching ISO 3166-2 subdivision format.
6. **Interface in Application/Common** — Follows the existing pattern where cross-cutting interfaces live in `Jobuler.Application.Common`.

## How it connects

- **Consumed by**: `LoginCommandHandler`, `RefreshTokenCommandHandler`, `UpdateUserLocationHandler` (tasks 2.1, 3.1, 3.2)
- **Depends on**: `CountryTimezoneMap` static data (task 1.3 populates full dataset)
- **Registered in**: `Program.cs` DI container

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with 0 errors. The resolver can be verified via the property tests in task 1.4.

## What comes next

- Task 1.3: Full `CountryTimezoneMap` static data population
- Task 1.4: Property tests for TimezoneResolver
- Task 2.1: `UpdateUserLocationCommand` handler that calls `ITimezoneResolver.Resolve`
- Task 3.1: Login handler integration

## Git commit

```bash
git add -A && git commit -m "feat(timezone): implement ITimezoneResolver interface and TimezoneResolver service"
```
