# 210 — Unavailability Reason Domain Entity

## Phase

Qualification Templates & Unavailability Reasons — Domain Layer

## Purpose

Introduces the `UnavailabilityReason` domain entity, which provides structured reasons for marking a person as unavailable. This entity is scoped to a space (tenant) and supports soft-delete via an `IsActive` flag. It serves as the foundation for the unavailability reason CRUD, template seeding, and presence window integration.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Spaces/UnavailabilityReason.cs` | New domain entity with `Create` factory, `Update`, and `Deactivate` methods |

## Key decisions

- Follows the same pattern as `HomeLeaveConfig` and `ConstraintRule`: private constructor, static `Create` factory, private setters, `Touch()` on mutations.
- `DisplayName` is validated at the domain level (max 100 chars, non-empty) — defense in depth alongside FluentValidation at the application layer.
- `Deactivate()` is a simple soft-delete (sets `IsActive = false`) without requiring a `userId` parameter, unlike `ConstraintRule` which tracks `UpdatedByUserId`. This keeps the entity simpler since audit logging will be handled separately.
- The entity trims whitespace from `DisplayName` on create and update.

## How it connects

- Implements `AuditableEntity` (provides `Id`, `CreatedAt`, `UpdatedAt`, `Touch()`) and `ITenantScoped` (provides `SpaceId` for tenant isolation).
- Will be referenced by `PresenceWindow` via an optional FK (`UnavailabilityReasonId`) in task 1.2.
- EF Core configuration and migration will be added in task 1.3.
- Application layer commands (CRUD + seed) will use this entity in tasks 2.1–2.5.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain/Jobuler.Domain.csproj
```

Build should succeed with zero errors.

## What comes next

- Task 1.2: Add `UnavailabilityReasonId` to `PresenceWindow` entity
- Task 1.3: EF Core configuration and migration

## Git commit

```bash
git add -A && git commit -m "feat(domain): add UnavailabilityReason entity for structured unavailability reasons"
```
