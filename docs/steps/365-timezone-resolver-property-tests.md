# 365 — Timezone Resolver Property Tests

## Phase

User Timezone Settings — Backend domain and infrastructure

## Purpose

Validates the correctness of the `TimezoneResolver` implementation using property-based tests (FsCheck). These tests ensure that the resolver always produces valid IANA timezone identifiers, that single-timezone countries are invariant to state input, and that computed UTC offsets are correct.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Domain/TimezoneResolverPropertyTests.cs` | FsCheck property tests covering Properties 4, 5, and 6 from the design document |

## Key decisions

- **Used FsCheck (already in project)** — The test project already had FsCheck 2.16.6 and FsCheck.Xunit installed, so no new dependencies were needed.
- **IANA validation approach** — On Windows, not all IANA timezone IDs are directly resolvable via `TimeZoneInfo.FindSystemTimeZoneById`. The validation helper combines system-resolvable IDs, IANA→Windows conversion, and the known IDs from `CountryTimezoneMap` (which are sourced from the IANA tzdata set) to form the complete valid set.
- **Smart generators** — Each property uses constrained generators that produce valid inputs from the actual `CountryTimezoneMap` data, ensuring tests exercise real mapping paths.
- **200 iterations per property** — Matches the project's existing convention for property test thoroughness.

## Properties tested

1. **Property 4: Timezone Resolver Output Validity** (3 sub-tests)
   - Country-only inputs always produce valid IANA IDs
   - Country+state inputs from StateMappings always produce valid IANA IDs
   - Random arbitrary state strings paired with valid countries still produce valid IANA IDs

2. **Property 5: Single-Timezone Country Invariant** (2 sub-tests)
   - Single-TZ countries always return their mapped timezone regardless of state input
   - Null state and arbitrary state produce identical results for single-TZ countries

3. **Property 6: Offset Computation Correctness** (3 sub-tests)
   - Computed offset matches independently-calculated expected offset for all mapped timezones
   - All offsets fall within the valid UTC range [-720, +840] minutes
   - Invalid/null country codes fall back to Asia/Jerusalem with correct offset

## How it connects

- Tests validate the `TimezoneResolver` (task 1.2) and `CountryTimezoneMap` (task 1.3) implementations
- Validates Requirements 3.1 (valid IANA output), 3.3 (single-TZ invariant), and 4.1 (offset correctness)
- Uses the same FsCheck patterns established in `BurdenScalingPropertyTests` and `SolverPayloadBurdenPropertyTests`

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~TimezoneResolverPropertyTests" --verbosity normal
```

Expected: 8 tests pass (200 iterations each for most properties).

## What comes next

- Task 2.3: Property tests for settings persistence and validation (Properties 2 and 3)
- Task 6.3: Frontend property tests for `formatLocalTime` (Properties 7 and 8)

## Git commit

```bash
git add -A && git commit -m "feat(timezone): property tests for TimezoneResolver (Properties 4, 5, 6)"
```
