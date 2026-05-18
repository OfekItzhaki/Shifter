# 366 — User Settings Persistence & Validation Property Tests

## Phase

Feature: User Timezone Settings — Task 2.3 (optional)

## Purpose

Validates that user location settings (Country/State) persist correctly through the domain entity and database round-trip, and that the FluentValidation validator correctly accepts valid ISO codes while rejecting invalid ones. These property-based tests provide confidence across the full input space rather than relying on a handful of examples.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Application/UserSettingsPersistencePropertyTests.cs` | FsCheck property tests covering Properties 2 and 3 from the design document |

### Property 2: User Settings Persistence Round-Trip

- `PersistAndReadBack_CountryOnly_ReturnsIdenticalCode` — random valid country codes persist and read back identically
- `PersistAndReadBack_CountryAndState_ReturnsIdenticalCodes` — random valid (country, state) pairs persist and read back identically
- `PersistAndReadBack_CaseInsensitive_NormalizesToUpperCase` — input in any casing normalizes to uppercase on persist

### Property 3: Geographic Code Validation

- `InvalidCountryCode_IsRejectedByValidator` — random non-ISO strings are rejected
- `ValidCountryCode_IsAcceptedByValidator` — all valid ISO country codes pass validation
- `InvalidStateForCountry_IsRejectedByValidator` — random strings that aren't valid states for a country are rejected
- `ValidStateForCountry_IsAcceptedByValidator` — all valid (country, state) pairs pass validation
- `StateForSingleTimezoneCountry_IsRejectedByValidator` — providing any state for a single-timezone country is rejected

## Key decisions

- Used FsCheck `Gen.Elements` to sample from the known-valid code sets in `ValidLocationCodes`
- Used `Arb.Default.NonEmptyString()` for generating random invalid inputs with a `where` filter
- Used EF Core InMemoryDatabase for persistence round-trip (consistent with existing test patterns)
- 100 iterations per property (matches design doc minimum)

## How it connects

- Tests validate the `UpdateUserLocationValidator` (FluentValidation) and `User.UpdateLocation()` domain method
- Depends on `ValidLocationCodes` static reference data
- Complements the existing `TimezoneResolverPropertyTests` (Properties 4–6)

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~UserSettingsPersistencePropertyTests" --verbosity normal
```

All 8 property tests should pass.

## What comes next

- Task 3.3: Unit tests for login and refresh timezone integration (already done)
- Task 4.2: Unit tests for UserSettingsController

## Git commit

```bash
git add -A && git commit -m "feat(user-timezone): property tests for settings persistence and validation"
```
