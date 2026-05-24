# 485 — Space Subscriptions EF Migration

## Phase

Space-Level Billing — Infrastructure Layer

## Purpose

Generate the EF Core migration that creates the `space_subscriptions` table in PostgreSQL. This table stores one subscription record per space, replacing the per-group billing model. The migration codifies the schema defined in the space-billing design document.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Persistence/Migrations/20260523205916_AddSpaceSubscriptions.cs` | EF Core migration creating the `space_subscriptions` table with all columns, PK, and indexes |
| `apps/api/Jobuler.Application/Persistence/Migrations/20260523205916_AddSpaceSubscriptions.Designer.cs` | Migration designer metadata (auto-generated) |
| `apps/api/Jobuler.Application/Persistence/Migrations/AppDbContextModelSnapshot.cs` | EF Core model snapshot reflecting the full database schema |
| `apps/api/Jobuler.Api/Jobuler.Api.csproj` | Added `Microsoft.EntityFrameworkCore.Design` package reference (required for migration tooling) |

## Key decisions

- **First migration**: This is the initial EF Core migration for the project. It captures the entire current schema as a baseline alongside the new `space_subscriptions` table.
- **Migration project**: Migrations live in `Jobuler.Application/Persistence/Migrations/` because `AppDbContext` is defined in the Application project (despite its `Jobuler.Infrastructure.Persistence` namespace). EF requires migrations in the same assembly as the DbContext.
- **Design package on Api project**: Added `Microsoft.EntityFrameworkCore.Design` to the startup project (`Jobuler.Api`) since the EF tools require it on the startup project to scaffold migrations.

## Table schema

| Column | Type | Constraints |
|--------|------|-------------|
| `id` | uuid | PK |
| `space_id` | uuid | NOT NULL |
| `tier_id` | text | NOT NULL |
| `status` | text | NOT NULL |
| `lemonsqueezy_subscription_id` | text | NULL |
| `lemonsqueezy_customer_id` | text | NULL |
| `trial_starts_at` | timestamptz | NOT NULL |
| `trial_ends_at` | timestamptz | NOT NULL |
| `current_period_start` | timestamptz | NULL |
| `current_period_end` | timestamptz | NULL |
| `peak_member_count` | integer | NOT NULL |
| `canceled_at` | timestamptz | NULL |
| `auto_renew` | boolean | NOT NULL |
| `created_at` | timestamptz | NOT NULL |
| `updated_at` | timestamptz | NOT NULL |

**Indexes:**
- `uq_space_subscriptions_space_id` — UNIQUE on `space_id`
- `idx_space_subscriptions_status` — non-unique on `status`

## How it connects

- Depends on: `SpaceSubscription` entity (task 1.1) and `SpaceSubscriptionConfiguration` (task 2.1)
- Enables: All application-layer commands and queries that persist/read space subscription data
- The unique index on `space_id` enforces the one-subscription-per-space invariant at the database level

## How to run / verify

```bash
# Apply the migration to a local PostgreSQL database
cd apps/api
dotnet ef database update --project Jobuler.Application --startup-project Jobuler.Api --context AppDbContext

# Verify the table exists
psql -h localhost -U jobuler -d jobuler -c "\d space_subscriptions"
```

## What comes next

- Application layer commands (task 3.3, 3.4) that read/write `SpaceSubscription` records
- Space subscription creation wired into the space creation flow (task 11.1)
- Webhook handlers that update subscription state (tasks 6.1–6.4)

## Git commit

```bash
git add -A && git commit -m "feat(billing): add EF migration for space_subscriptions table"
```
