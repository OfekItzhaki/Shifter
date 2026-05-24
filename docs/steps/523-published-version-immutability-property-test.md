# Step 523 — Published Version Immutability Property Test

## Phase

Phase: Schedule Regeneration — Property-Based Testing

## Purpose

Validates that the published schedule version remains completely immutable throughout the entire regeneration lifecycle. This is a critical safety invariant: no matter what happens during regeneration (run creation, solver success, solver failure, draft discard), the currently published version's status and assignment rows must never change.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Scheduling/PublishedVersionImmutabilityPropertyTests.cs` | FsCheck property test (100 iterations) + 6 deterministic examples verifying published version immutability |

## Key decisions

- **Event-driven approach**: The test generates random sequences of regeneration lifecycle events (run creation, solver success, solver failure, draft discard) and verifies the invariant holds after each event in the sequence.
- **Content verification**: Beyond checking status, the test verifies that the exact set of (TaskSlotId, PersonId) pairs in the published version's assignments remains unchanged — not just the count.
- **Composite lifecycle sequences**: The property test generates sequences of 1–5 events, testing that immutability holds through multi-step lifecycles (e.g., create → fail → create → succeed → discard).
- **No mocking**: Tests exercise real domain entities and EF Core InMemory database to validate actual behavior.

## How it connects

- Validates Requirements 3.2 (published version unchanged on success), 3.3 (published version unchanged on failure), 3.5 (published version remains active until explicit publish), 5.4 (archival preserves historical record), 6.2 (discard leaves published version intact)
- Complements Property 2 (draft creation) and Property 3 (failure recording) by verifying the "other side" — that the published version is never a victim of these operations
- Uses the same `ScheduleVersion.CreateRegenerationDraft`, `ScheduleRun.Create`, and `Discard()` code paths as the production worker and command handlers

## How to run / verify

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~PublishedVersionImmutabilityPropertyTests" --no-restore
```

Expected: 7 tests pass (1 property test × 100 iterations + 6 deterministic examples).

## What comes next

- Task 7.3: Property test for failed regeneration recording error without side effects
- Task 9.3: Property test for regeneration not blocking standard runs
- Task 5.2: Property test for permission enforcement

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): property test for published version immutability"
```
