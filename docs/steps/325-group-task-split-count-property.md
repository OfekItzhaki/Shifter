# Step 325: GroupTask Domain Entity — SplitCount Property

## Phase
Feature — Split-Burden Scaling

## Purpose
Adds the `SplitCount` property to the `GroupTask` domain entity so the system can track how many sub-shifts a task is divided into. This is the domain-level counterpart to the database column added in step 324. The property is used downstream by `BurdenScalingService` to compute effective burden levels.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Tasks/GroupTask.cs` | Added `SplitCount` property (default 1), updated `Create` factory method and `Update` method to accept `int splitCount = 1` with validation |

## Key decisions
- Used a default parameter value (`int splitCount = 1`) so all existing callers compile without changes
- Validation throws `ArgumentOutOfRangeException` when `splitCount < 1`, consistent with domain validation patterns used elsewhere (e.g., `FairnessCounter`, `PresenceWindow`)
- Property placed after `QualificationRequirements` and before `IsActive` to group it with other task configuration fields
- No external dependencies added — Domain layer remains pure

## How it connects
- **Requirement 1.1**: Persists split count via the entity property
- **Requirement 1.2**: Default value of 1 means unsplit tasks work correctly
- **Requirement 1.3**: `Update` method allows changing split count when admin merges/splits sub-shifts
- **Depends on**: Step 324 (database column exists)
- **Next**: Task 1.4 (EF Core mapping of `SplitCount` → `split_count` column)

## How to run / verify
```bash
# Build the Domain project
cd apps/api/Jobuler.Domain && dotnet build

# Build the full solution to confirm no breaking changes
cd apps/api && dotnet build
```

## What comes next
- Task 1.4: Map `SplitCount` to `split_count` column in EF Core configuration
- Task 1.5: Property-based tests for BurdenScalingService
- Task 1.6: Unit tests for BurdenScalingService

## Git commit
```bash
git add -A && git commit -m "feat(split-burden): add SplitCount property to GroupTask domain entity"
```
