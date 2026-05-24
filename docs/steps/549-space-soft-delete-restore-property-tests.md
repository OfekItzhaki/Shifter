# 549 — Space Soft-Delete/Restore Property Tests

## Phase

Space Management — Application Layer Testing

## Purpose

Implements property-based tests (FsCheck + xUnit) for the soft-delete/restore domain behavior, validating three correctness properties from the design document: round-trip invariant, cascade preservation of individually-deleted groups, and listing exclusion of soft-deleted spaces.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Tests/Domain/SpaceSoftDeletePropertyTests.cs` | 8 property-based tests covering Properties 1, 2, and 3 from the space-management design |

## Key decisions

- **Pure domain-level tests**: All tests exercise `Space` and `Group` entity methods directly without database or mocking — they validate domain logic in isolation.
- **FsCheck generators**: Used `Gen.Choose` for bounded integer generation (group counts, deleted counts) and `NonEmptyString` for space names.
- **100 iterations minimum**: Each `[Property(MaxTest = 100)]` ensures sufficient coverage per the spec requirement.
- **Trait tagging**: All tests tagged with `[Trait("Feature", "space-management")]` for filtering.

## How it connects

- Validates the `Space.SoftDelete()` / `Space.Restore()` methods added in task 1.1
- Validates the `Group.SoftDeleteBySpace()` / `Group.RestoreFromSpaceDeletion()` methods added in task 1.2
- Validates the listing filter pattern used in task 11.5 (exclude soft-deleted spaces)
- Complements the existing unit tests in `SpaceTests.cs`

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~SpaceSoftDeletePropertyTests" --verbosity normal
```

Expected: 8 tests pass (Property 1: 2 tests, Property 2: 3 tests, Property 3: 3 tests).

## What comes next

- Task 6.2: Property tests for ownership transfer (Properties 5, 6, 7)
- Task 7.6: Property tests for settings commands (Properties 8, 9, 10, 11)

## Git commit

```bash
git add -A && git commit -m "feat(space-management): property tests for soft-delete/restore (Properties 1, 2, 3)"
```
