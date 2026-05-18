# 355 — Country Timezone Map Static Data

## Phase

User Timezone Settings — Backend Domain & Infrastructure (Task 1.3)

## Purpose

Provides the static lookup data that the `TimezoneResolver` uses to map a user's Country/State geographic selection to an IANA timezone identifier. This eliminates the need for users to manually pick from a long timezone list — they select their country (and optionally state), and the system resolves the correct timezone automatically.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Timezone/CountryTimezoneMap.cs` | Static class containing all timezone mapping dictionaries |

### Key data structures

- **`CountryMappings`** — Dictionary mapping country codes to IANA timezone IDs. Contains both single-timezone countries (direct mapping) and multi-timezone countries (most-populous-timezone fallback).
- **`StateMappings`** — Dictionary mapping `"CC-STATE"` keys to IANA timezone IDs for multi-timezone countries (US, RU, AU, CA, BR, MX, ID, CL, KZ, CN, MN, CD, AR, PT, ES, NZ).
- **`MultiTimezoneCountries`** — HashSet of country codes that span multiple timezones, used by the frontend to decide whether to show the State/Region dropdown.
- **`IsMultiTimezoneCountry()`** — Helper method to check if a country needs state-level resolution.

## Key decisions

1. **String key format `"CC-STATE"` for StateMappings** — Matches the existing `TimezoneResolver` implementation which constructs keys as `$"{countryCode}-{stateCode}"`.
2. **Case-insensitive comparers** — Both dictionaries use `StringComparer.OrdinalIgnoreCase` so lookups work regardless of input casing.
3. **Multi-timezone fallbacks in CountryMappings** — Rather than a separate fallback dictionary, multi-timezone countries are included in `CountryMappings` with their most-populous timezone. The resolver tries state first, then falls back to country.
4. **Coverage scope** — Includes ~150 single-timezone countries and 16 multi-timezone countries with state-level mappings for their major subdivisions.
5. **Source data** — Based on IANA Time Zone Database (tzdata) maintained by ICANN.

## How it connects

- **Consumed by**: `TimezoneResolver` (Infrastructure) — uses `StateMappings` and `CountryMappings` to resolve timezone from user's geographic selection
- **Supports**: `UpdateUserLocationCommand` (Application) — validates that country/state combinations are meaningful
- **Frontend dependency**: `MultiTimezoneCountries` set determines when the State/Region dropdown is shown in the Settings page
- **Fallback chain**: State → Country (most populous TZ) → Asia/Jerusalem (handled by resolver, not this map)

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

The class is purely static data with no runtime dependencies. Correctness is validated by the property tests in task 1.4.

## What comes next

- Task 1.2 (TimezoneResolver) consumes this map — already implemented and building successfully
- Task 1.4 writes property tests validating that all mapped values are valid IANA timezone IDs
- Task 2.1 (UpdateUserLocationCommand) uses `IsMultiTimezoneCountry` for validation logic

## Git commit

```bash
git add -A && git commit -m "feat(timezone): add CountryTimezoneMap static data for timezone resolution"
```
