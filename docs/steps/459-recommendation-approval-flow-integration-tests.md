# 459 — Recommendation Approval Flow Integration Tests

## Phase

Feature: recommendation-approval-flow (Task 8.4)

## Purpose

Validates the end-to-end behavior of the recommendation approval flow refactoring through integration tests. These tests confirm that:
1. The accept endpoint is fully removed (returns 404)
2. The schedule endpoint includes `taskConfigurations` in its response
3. The recommendation engine still generates recommendations after the refactor
4. The task update endpoint remains the only way to enable double shift

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Integration/RecommendationApprovalFlowIntegrationTests.cs` | 9 integration tests covering all four integration test scenarios from the design document |

## Key decisions

- Used the same in-memory EF Core pattern as `AdminManagementIntegrationTests` for consistency
- Verified accept endpoint removal via reflection (checking controller methods and assembly types) rather than HTTP calls, since the project doesn't use WebApplicationFactory
- Tested the recommendation engine directly with a realistic scenario (uncovered slots + home leave causing shortfall)
- Verified that dismiss does NOT modify `AllowsDoubleShift` while the update command DOES

## How it connects

- Validates Requirements 4.1, 4.3, 4.4, 7.1, 7.2 from the spec
- Depends on tasks 1.1 (accept endpoint removal), 1.2 (EnableDoubleShift removal), 1.3 (TaskConfigSummaryDto and schedule response extension)
- Completes the integration wiring phase (task group 8) of the recommendation approval flow

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~RecommendationApprovalFlowIntegrationTests" --verbosity normal
```

All 9 tests should pass.

## What comes next

- Task 9: Final checkpoint — ensure all tests pass across the entire test suite

## Git commit

```bash
git add -A && git commit -m "feat(recommendation-approval-flow): integration tests for refactored recommendation system"
```
