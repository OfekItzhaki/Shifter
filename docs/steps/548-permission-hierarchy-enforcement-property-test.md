# Step 548 — Permission Hierarchy Enforcement Property Test

## Phase

Phase: Space Management — Property-Based Testing

## Purpose

Validates Property 4 from the space-management design: the four-tier permission hierarchy (SpaceOwner > GroupOwner > Admin > Member) is correctly enforced by `PermissionService`. Users at level L are denied actions requiring level L' > L and permitted actions requiring level L' ≤ L.

## What was built

- `Jobuler.Tests/Application/PermissionHierarchyEnforcementPropertyTests.cs` — FsCheck property tests (4 properties, 100 iterations each):
  - `User_Below_Required_Level_Is_Denied` — verifies denial when user level < required level
  - `User_At_Or_Above_Required_Level_Is_Permitted` — verifies access when user level ≥ required level
  - `SpaceOwner_Has_All_Permissions` — verifies SpaceOwner implicitly holds all permission keys
  - `RequirePermission_Throws_When_Denied` — verifies `RequirePermissionAsync` throws `UnauthorizedAccessException`

## Key decisions

- Tests use EF Core InMemory database with real `PermissionService` (no mocks for the SUT)
- Permission keys are categorized into OwnerOnly (level 3) and Admin (level 1+) sets matching the service implementation
- GroupOwner level seeds a Person + GroupMembership with `IsOwner=true` to satisfy the `IsGroupOwnerInSpaceAsync` check
- Each test iteration creates a fresh in-memory DB to avoid cross-test contamination

## How it connects

- Validates the `PermissionService` implementation from step 529
- Covers Requirements 4.1, 4.2, 4.3, 4.4, 4.5, 4.7

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~PermissionHierarchyEnforcementPropertyTests"
```

## What comes next

- Property tests for soft-delete/restore (Properties 1, 2, 3) in task 5.3
- Property tests for ownership transfer (Properties 5, 6, 7) in task 6.2

## Git commit

```bash
git add -A && git commit -m "feat(space-management): permission hierarchy enforcement property test (Property 4)"
```
