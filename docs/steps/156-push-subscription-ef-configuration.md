# 156 — Push Subscription EF Core Configuration & Migration

## Phase
Push Notifications — Persistence Layer

## Purpose
Configures EF Core to map the `PushSubscription` domain entity to the `push_subscriptions` PostgreSQL table, with proper snake_case column naming, a unique constraint on `(user_id, space_id, endpoint)` to prevent duplicate subscriptions per device, and a composite index on `(user_id, space_id)` for efficient lookup during push delivery.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/PushSubscriptionConfiguration.cs` | Fluent API configuration mapping PushSubscription entity to `push_subscriptions` table with unique constraint and index |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Added `DbSet<PushSubscription> PushSubscriptions` property |
| `infra/migrations/040_push_subscriptions.sql` | SQL migration creating the table, unique constraint, index, and RLS policy |

## Key decisions
- Followed existing pattern from `NotificationConfiguration.cs` for column naming and structure
- Added RLS policy for tenant isolation (consistent with security rules)
- Used `ON DELETE CASCADE` for both `space_id` and `user_id` foreign keys — when a space or user is deleted, their push subscriptions are automatically cleaned up
- Unique constraint named `uq_push_sub_user_space_endpoint` matches the design document exactly

## How it connects
- The `PushSubscription` entity was created in step 154
- This configuration enables EF Core to persist and query subscriptions
- The `IPushNotificationSender` (step 155) will query `PushSubscriptions` DbSet to find delivery targets
- The upcoming `PushSubscriptionsController` will use MediatR commands that interact with this DbSet

## How to run / verify
```bash
# Build the solution
cd apps/api && dotnet build

# Run the migration against a local database
psql -U postgres -d jobuler -f infra/migrations/040_push_subscriptions.sql
```

## What comes next
- MediatR commands for creating/deleting push subscriptions
- PushSubscriptionsController REST endpoints
- Integration of push delivery into NotificationService

## Git commit
```bash
git add -A && git commit -m "feat(push): add EF Core configuration and migration for push_subscriptions table"
```
