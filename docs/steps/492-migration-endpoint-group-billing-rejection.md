# 492 — Migration Endpoint and Post-Migration Group Billing Rejection

## Phase

Space-Level Billing — API Layer

## Purpose

Provides a platform-admin-only endpoint to trigger the one-time migration from group-level billing to space-level billing, and ensures that after migration, all legacy group-level billing endpoints return `410 Gone` to signal clients to use the new space-level endpoints.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/PlatformController.cs` | Added `POST /platform/billing/migrate` endpoint (admin-only) that dispatches `MigrateToSpaceBillingCommand` and returns `MigrationResult` as JSON. Added `MigrateBillingRequest` DTO with optional `BatchSize`. |
| `apps/api/Jobuler.Api/Controllers/BillingController.cs` | Added `AppDbContext` dependency. Added `RejectIfMigratedAsync` helper that checks for a `SpaceSubscription` and returns 410 Gone. Applied the check to all four group-level endpoints: `GetSubscription`, `CancelSubscription`, `RenewSubscription`, `CreateCheckout`. |

## Key decisions

- **Migration endpoint on PlatformController** — Follows the existing pattern where all platform-admin-only operations live under `/platform/*` with `IsPlatformAdmin` checks. The route is `POST /platform/billing/migrate` rather than `/admin/billing/migrate` for consistency.
- **410 Gone for group-level endpoints** — The check queries `SpaceSubscriptions.Any(s => s.SpaceId == spaceId)`. If a SpaceSubscription exists for the space, the group-level operation is rejected. This is a lightweight DB check that runs before any business logic.
- **Optional batchSize** — The request body is optional; defaults to 100 if not provided, matching the command's default.

## How it connects

- The `MigrateToSpaceBillingCommand` (task 9.1) handles the actual migration logic — marking GroupSubscriptions as "migrated" and creating SpaceSubscriptions.
- The 410 Gone logic fulfills Requirement 8.6: after migration, group-level billing operations are rejected.
- Frontend clients receiving 410 should redirect to the space-level billing endpoints added in task 10.1.

## How to run / verify

1. Build the API: `dotnet build apps/api/Jobuler.Api/Jobuler.Api.csproj`
2. Start the API and authenticate as a platform admin.
3. Call `POST /platform/billing/migrate` with optional body `{ "batchSize": 50 }` — should return migration results.
4. After migration, call any group-level billing endpoint (e.g., `GET /spaces/{id}/billing/groups/{gid}/subscription`) — should return 410 Gone.

## What comes next

- Task 10.3: Property test for webhook signature rejection (Property 17)
- Task 11.1: Wire space subscription creation into space creation flow
- Frontend updates to handle 410 responses and redirect to space-level billing

## Git commit

```bash
git add -A && git commit -m "feat(space-billing): add migration endpoint and 410 Gone for group billing"
```
