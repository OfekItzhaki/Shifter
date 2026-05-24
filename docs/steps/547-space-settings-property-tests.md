# Step 547 — Space Settings Property Tests (Properties 8, 9, 10, 11)

## Phase

Space Management — Property-Based Testing

## Purpose

Validates correctness properties for space-level settings commands using FsCheck property-based testing. Ensures management timeout validation, timeout propagation semantics, space name validation, and invite code regeneration all behave correctly across all valid and invalid inputs.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Tests/Domain/SpaceSettingsPropertyTests.cs` | Property-based tests covering Properties 8, 9, 10, 11 from the space-management design |

## Key decisions

- **Domain-level testing for Properties 8, 9, 11**: These properties test pure domain logic (`Space.SetManagementTimeout`, `Space.RegenerateInviteCode`) directly without needing infrastructure.
- **Validator-level testing for Property 10**: Name validation is tested via `UpdateSpaceCommandValidator` since that's where the trim + length check is enforced at the application boundary.
- **FsCheck generators constrain input space**: Custom `Arbitrary<T>` generators produce valid/invalid values within meaningful ranges rather than relying on default generators.
- **100 iterations minimum**: All `[Property]` tests use `MaxTest = 100` as specified in the design document.

## How it connects

- Tests validate the `Space` domain entity methods implemented in task 1.1
- Tests validate the `UpdateSpaceCommandValidator` implemented in task 7.4
- Tests validate the `RegenerateInviteCode` method used by task 7.5
- Complements existing unit tests in `SpaceTests.cs` with exhaustive property coverage

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~SpaceSettingsPropertyTests" --verbosity normal
```

All 8 property tests should pass (2 for Property 8, 2 for Property 9, 2 for Property 10, 2 for Property 11).

## What comes next

- Task 10.3: Property tests for propagation and audit (Properties 12, 13)
- Frontend unit tests for settings components

## Git commit

```bash
git add -A && git commit -m "feat(space-management): property tests for settings commands (Properties 8, 9, 10, 11)"
```
