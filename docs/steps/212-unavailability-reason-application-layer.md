# 212 — Unavailability Reason Application Layer Commands & Queries

## Phase

Phase 9 — Qualification Templates & Unavailability Reasons

## Purpose

Implements the full CQRS application layer for the `UnavailabilityReason` entity: a query to list active reasons, commands to create/update/deactivate/seed reasons, and extends the existing `AddPresenceWindowCommand` to accept an optional `ReasonId` with validation.

## What was built

| File | Description |
|------|-------------|
| `Application/Spaces/Queries/GetUnavailabilityReasonsQuery.cs` | MediatR query that returns active reasons for a space, ordered by SortOrder |
| `Application/Spaces/Commands/CreateUnavailabilityReasonCommand.cs` | Creates a single reason; enforces 50-reason-per-space limit |
| `Application/Spaces/Commands/UpdateUnavailabilityReasonCommand.cs` | Updates DisplayName and SortOrder; throws KeyNotFoundException if not found |
| `Application/Spaces/Commands/DeactivateUnavailabilityReasonCommand.cs` | Soft-deletes a reason via `Deactivate()`; throws KeyNotFoundException if not found |
| `Application/Spaces/Commands/SeedUnavailabilityReasonsCommand.cs` | Bulk-creates reasons with sequential sort order; no-op if space already has reasons |
| `Application/People/Commands/AddPresenceWindowCommand.cs` | Extended with optional `Guid? ReasonId`; validates reason exists and is active in space |
| `Application/Spaces/Validators/CreateUnavailabilityReasonCommandValidator.cs` | FluentValidation: DisplayName required, max 100 chars, SortOrder ≥ 0 |
| `Application/Spaces/Validators/UpdateUnavailabilityReasonCommandValidator.cs` | FluentValidation: same rules as create + ReasonId required |
| `Application/Spaces/Validators/SeedUnavailabilityReasonsCommandValidator.cs` | FluentValidation: each display name required, max 100 chars |

## Key decisions

- **No permission checks in handlers**: Following the existing pattern (e.g., `CreateSpaceRoleCommand`), permission checks are done at the controller level, not in command handlers. The controller will call `IPermissionService.RequirePermissionAsync` before dispatching.
- **50-reason limit enforced in handler**: The max-50 constraint is checked at the application layer (not DB constraint) to provide a clear error message.
- **Seed is idempotent**: If the space already has any active reasons, the seed command is a no-op — this prevents template re-application from duplicating reasons.
- **ReasonId validation in AddPresenceWindowCommand**: The handler validates the reason exists, is active, and belongs to the same space before creating the presence window. Invalid reasons throw `KeyNotFoundException` which maps to 404 via middleware.

## How it connects

- These commands/queries are consumed by the `UnavailabilityReasonsController` (Task 3.1) and the extended `AvailabilityController` (Task 3.2).
- The `SeedUnavailabilityReasonsCommand` is called by the frontend's `GroupTemplatePicker` during template application (Task 5.3).
- The `AddPresenceWindowCommand` extension enables the unavailability form to attach structured reasons to presence windows (Task 7.2).

## How to run / verify

```bash
cd apps/api
dotnet build  # All projects should compile cleanly
dotnet test   # Existing tests should pass
```

## What comes next

- Task 3.1: Create `UnavailabilityReasonsController` with CRUD endpoints
- Task 3.2: Extend `AvailabilityController` to pass `ReasonId` through

## Git commit

```bash
git add -A && git commit -m "feat(qualification-templates): unavailability reason application layer commands and queries"
```
