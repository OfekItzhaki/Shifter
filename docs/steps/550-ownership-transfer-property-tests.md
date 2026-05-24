# 550 — Ownership Transfer Property Tests

## Phase

Space Management — Property-Based Testing

## Purpose

Implements property-based tests (Properties 5, 6, 7 from the design document) that verify the correctness of the `TransferOwnershipCommand` handler. These tests use FsCheck to generate randomized inputs and verify universal properties hold across all valid scenarios.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Application/OwnershipTransferPropertyTests.cs` | Property-based tests for ownership transfer using FsCheck.Xunit |

### Properties tested

- **Property 5**: Transfer updates `Space.OwnerUserId` to the target user and creates an `OwnershipTransferHistory` record with correct previous owner, new owner, requesting user, and reason fields.
- **Property 6**: After transfer, the new owner has all 13 defined permission keys granted in `SpacePermissionGrant` (no revoked grants).
- **Property 7**: Transfer to a user who is not an active member (either not a member at all, or an inactive member) throws `InvalidOperationException` with the expected message.

## Key decisions

- Used `Prop.ForAll` with synchronous `.GetAwaiter().GetResult()` pattern because FsCheck 2.x does not natively support `async Task<bool>` as a testable type.
- Each test uses a fresh in-memory database (`Guid.NewGuid()` as DB name) for isolation.
- `IPermissionService` and `IAuditLogger` are mocked with NSubstitute (allow-all and no-op respectively) since these tests focus on the handler's core logic, not authorization or audit.
- Custom FsCheck arbitraries generate varied inputs: 1–5 members, random target indices, optional reasons, and inactive member scenarios.
- Minimum 100 iterations per property test via `[Property(MaxTest = 100)]`.

## How it connects

- Tests validate the `TransferOwnershipCommand` handler implemented in task 6.1.
- Validates Requirements 3.1, 3.2, 3.3, 3.5, 3.6 from the space-management spec.
- Uses the same in-memory database pattern as other property tests in the project.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~OwnershipTransferPropertyTests"
```

Expected: 3 tests pass (Property5, Property6, Property7), each running 100 iterations.

## What comes next

- Task 7.6: Property tests for settings commands (Properties 8, 9, 10, 11)
- Task 10.3: Property tests for propagation and audit (Properties 12, 13)

## Git commit

```bash
git add -A && git commit -m "feat(space-management): ownership transfer property tests (Properties 5, 6, 7)"
```
