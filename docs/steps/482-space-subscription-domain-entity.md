# 482 — SpaceSubscription Domain Entity

## Phase

Space-Level Billing — Domain Layer

## Purpose

Introduces the `SpaceSubscription` entity that replaces per-group billing with a single subscription per space. This is the foundational domain model for the entire space-billing feature.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Billing/SpaceSubscription.cs` | New domain entity with factory method, state transitions, guard clauses, and computed properties |

## Key decisions

- Extends `AuditableEntity` (provides `Id`, `CreatedAt`, `UpdatedAt`) and implements `ITenantScoped` (provides `SpaceId` for RLS).
- Uses the existing `SubscriptionStatus` enum from `GroupSubscription.cs` — no duplication.
- `CreateTrial` factory method captures `TrialStartsAt` and `TrialEndsAt` at creation time.
- Guard clauses throw `InvalidOperationException` for invalid state transitions (maps to 400 via `ExceptionHandlingMiddleware`).
- `IsAccessGranted` grants access for Active, non-expired Trialing, and Canceled within grace period (CurrentPeriodEnd > now).
- `DaysRemaining` uses `Math.Ceiling` to compute days until trial end or period end depending on status.
- `RenewWithinGracePeriod` preserves existing period dates; `RenewAfterExpiry` sets new dates.
- Domain layer has zero external dependencies — pure C# only.

## How it connects

- Used by Application layer commands (checkout, cancel, renew, webhook handlers) in later tasks.
- EF Core configuration (task 2.1) will map this entity to the `space_subscriptions` table.
- Property tests (tasks 1.3–1.5) will validate state transition correctness.

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build --no-restore
```

## What comes next

- Task 1.2: Add `Migrated` value to `SubscriptionStatus` enum.
- Task 1.3–1.5: Property-based tests for the entity.
- Task 2.1: EF Core configuration for persistence.

## Git commit

```bash
git add -A && git commit -m "feat(billing): add SpaceSubscription domain entity with lifecycle methods"
```
