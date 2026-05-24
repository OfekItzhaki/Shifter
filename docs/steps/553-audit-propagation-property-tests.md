# Step 553 — Audit Logging & Home-Leave Propagation Property Tests

## Phase
Phase 10 — Space Management (Infrastructure: Solver Payload & Audit Integration)

## Purpose
Implements property-based tests (Properties 12 and 13 from the space-management design) that verify:
1. Every auditable space management action (soft-delete, restore, transfer, role assign) produces an audit log entry with actor, space ID, action name, and timestamp.
2. Space-level home-leave configuration propagates correctly to solver payloads, overriding group-level values.

## What was built
- `Jobuler.Tests/Application/AuditAndPropagationPropertyTests.cs` — FsCheck property tests with 100 iterations each:
  - **Property 12** (4 test methods): Verifies IAuditLogger is called with correct spaceId, actorUserId, and action name for soft-delete, restore, ownership transfer, and role assignment commands.
  - **Property 13** (2 test methods): Verifies the SolverPayloadNormalizer uses space-level SpaceHomeLeaveConfig values (mode, base days, balance value, leave duration, leave capacity) in the solver payload. Also tests emergency freeze mode produces correct override values (balance=0, threshold=9999).

## Key decisions
- Used NSubstitute mocks for IAuditLogger to verify calls without needing a real database audit table.
- Property 13 tests the full SolverPayloadNormalizer.BuildAsync flow with an in-memory database, seeding space-level config, closed-base groups, and group members.
- Generated constrained inputs via FsCheck arbitraries that respect domain validation rules (e.g., leave duration 12–168h, balance 0–100, base days ≥ 1).

## How it connects
- Validates Requirements 1.5, 2.5, 3.7 (audit logging) and 6.2, 6.3, 6.5 (home-leave propagation).
- Depends on task 10.1 (solver payload normalizer space-level config) and 10.2 (audit logging implementation).
- Completes the property test coverage for the space-management spec's infrastructure layer.

## How to run / verify
```bash
cd apps/api
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~AuditAndPropagationPropertyTests" --verbosity normal
```

## What comes next
- API layer endpoints (task 11.x) and frontend components (tasks 13–18).

## Git commit
```bash
git add -A && git commit -m "feat(space-management): property tests for audit logging and home-leave propagation (Properties 12, 13)"
```
