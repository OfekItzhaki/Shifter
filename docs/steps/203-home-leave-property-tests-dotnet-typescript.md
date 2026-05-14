# Step 203 — Home-Leave Property-Based Tests (.NET & TypeScript)

## Phase

Phase 6 — Property-Based Testing

## Purpose

Implements property-based tests for home-leave config validation (Property 1), template name validation (Property 11), and fairness warning threshold (Property 10). These tests verify that validation logic correctly accepts valid inputs and rejects invalid inputs across a wide range of generated values.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/HomeLeave/HomeLeaveConfigValidationPropertyTests.cs` | Property 1: Tests config validation with 200 random tuples + boundary cases for min_rest_hours, eligibility_threshold_hours, leave_capacity, and leave_duration_hours |
| `apps/api/Jobuler.Tests/HomeLeave/HomeLeaveTemplateNamePropertyTests.cs` | Property 11: Tests template name validation with 200 random strings + boundary cases for length limits and whitespace rules |
| `apps/web/__tests__/home-leave/fairnessWarning.property.test.ts` | Property 10: Tests fairness warning threshold using fast-check with 500+ generated arrays of base_time_ratio values |

## Key decisions

- Used xUnit `[Theory]`/`[InlineData]` with a deterministic LCG-based random generator instead of FsCheck (not in project dependencies)
- Used fast-check (already in devDependencies) for the TypeScript property test
- Extracted the fairness warning logic as a pure function to test independently of React rendering
- Validator-only testing for .NET (the upper bound of leave_capacity is checked in the handler with DB access, not the validator)

## How it connects

- Tests validate the `UpsertHomeLeaveConfigValidator` and `CreateHomeLeaveTemplateCommandValidator` from Task 3.1 and 4.1
- TypeScript test validates the logic in `HomeLeaveMetricsPanel.tsx` from Task 13.1
- All tests reference specific requirements from the spec (2.4, 2.5, 2.6, 2.7, 9.4, 10.8)

## How to run / verify

```bash
# .NET tests (61 tests)
cd apps/api
dotnet test --filter "FullyQualifiedName~HomeLeave" --no-restore

# TypeScript test (requires node_modules installed)
cd apps/web
npm run test -- __tests__/home-leave/fairnessWarning.property.test.ts
```

## What comes next

- All property-based tests for the home-leave feature are now complete (Tasks 15 + 16)
- Final integration verification (Task 17)

## Git commit

```bash
git add -A && git commit -m "feat(phase6): property-based tests for home-leave config, template name, and fairness warning"
```
