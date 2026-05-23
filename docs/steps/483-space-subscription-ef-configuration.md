# 483 — Space Subscription EF Configuration

## Phase

Space-Level Billing — Infrastructure Layer

## Purpose

Configures Entity Framework Core mapping for the `SpaceSubscription` domain entity to the `space_subscriptions` PostgreSQL table. This enables persistence of space-level billing data with proper column naming, indexes, and type conversions.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/SpaceSubscriptionConfiguration.cs` | EF Core `IEntityTypeConfiguration<SpaceSubscription>` mapping all properties to snake_case columns, configuring string conversion for `Status`, and defining indexes |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Registered `DbSet<SpaceSubscription> SpaceSubscriptions` in the Billing section |

## Key decisions

- Followed the same coding style as `GroupSubscriptionConfiguration` (same file structure, property ordering, naming conventions)
- Added `IsRequired()` on non-nullable columns (`SpaceId`, `TierId`, `Status`, `TrialStartsAt`, `TrialEndsAt`, `PeakMemberCount`, `AutoRenew`, `CreatedAt`, `UpdatedAt`) to match the database schema constraints
- Used `HasConversion<string>()` on `Status` to store the enum as a readable string in PostgreSQL
- Created a unique index `uq_space_subscriptions_space_id` enforcing one subscription per space
- Created an index `idx_space_subscriptions_status` for efficient expiry job queries

## How it connects

- Depends on: `SpaceSubscription` domain entity (task 1.1, step 482)
- Used by: EF migration (task 2.2), all commands/queries that persist or read `SpaceSubscription` records
- The configuration is auto-discovered via `ApplyConfigurationsFromAssembly` in `AppDbContext.OnModelCreating`

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build succeeds with no errors related to the new configuration.

## What comes next

- Task 2.2: Generate EF migration (`dotnet ef migrations add AddSpaceSubscriptions`)
- Task 2.3: Update `GroupSubscription` EF configuration to support `Migrated` status value

## Git commit

```bash
git add -A && git commit -m "feat(billing): add SpaceSubscription EF configuration and DbSet registration"
```
