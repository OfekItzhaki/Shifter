# 213 — Unavailability Reasons API Layer

## Phase

Feature: Qualification Templates & Unavailability Reasons

## Purpose

Exposes the unavailability reason CRUD operations and seed endpoint via a REST controller, extends the AvailabilityController to accept an optional `ReasonId` when creating presence windows, and enriches the presence window GET response with reason data (id + display name).

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Api/Controllers/UnavailabilityReasonsController.cs` | New controller with GET (list), POST (create), PUT (update), DELETE (deactivate), POST /seed endpoints. All require `[Authorize]` and `PeopleManage` permission. |
| `Jobuler.Api/Controllers/AvailabilityController.cs` | Extended `AddPresenceRequest` record to include `Guid? ReasonId`. Passes it through to `AddPresenceWindowCommand`. |
| `Jobuler.Application/People/Queries/GetAvailabilityQuery.cs` | Extended `PresenceWindowDto` with `ReasonId` and `ReasonDisplayName`. Updated `GetPresenceQueryHandler` to left-join with `UnavailabilityReasons` table. |

## Key decisions

- Used `PeopleManage` permission for all unavailability reason endpoints (same permission as presence window management) since reasons are tightly coupled to the unavailability workflow.
- Used a left join (GroupJoin + SelectMany with DefaultIfEmpty) in the GET presence query to include the reason display name without requiring a navigation property on the entity.
- `ReasonId` is optional with a default of `null` on the request record to maintain backward compatibility with existing clients.
- The `PresenceWindowDto` uses optional parameters with defaults for `ReasonId` and `ReasonDisplayName` to avoid breaking existing consumers.

## How it connects

- Depends on: Domain entity (step 210), EF migration (step 211), Application layer commands/queries (step 212)
- Used by: Frontend unavailability form (task 7), frontend settings panel (task 6), template picker (task 5.3)

## How to run / verify

```bash
cd apps/api
dotnet build   # should succeed with 0 errors
dotnet test    # all non-solver tests pass (solver tests require localhost:8000)
```

## What comes next

- Frontend template data extension (task 5.1)
- Frontend unavailability reason settings panel (task 6)
- Frontend unavailability form update (task 7)

## Git commit

```bash
git add -A && git commit -m "feat(api): unavailability reasons controller and presence window reason support"
```
