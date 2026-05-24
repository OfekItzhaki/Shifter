# 528 — Space Home Leave Config EF Configuration

## Phase

Space Management — Infrastructure Layer (Persistence)

## Purpose

Provides the Entity Framework Core configuration for the `SpaceHomeLeaveConfig` domain entity, mapping it to the `space_home_leave_configs` PostgreSQL table with snake_case column names, int-based enum conversion for `Mode` and `PreFreezeMode`, and a unique index on `space_id`. Also registers the `DbSet<SpaceHomeLeaveConfig>` in `AppDbContext` so the entity can be queried and persisted.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Infrastructure/Persistence/Configurations/SpaceHomeLeaveConfigConfiguration.cs` | EF Core `IEntityTypeConfiguration<SpaceHomeLeaveConfig>` — maps all properties to snake_case columns, configures `Mode` and `PreFreezeMode` as int conversions, adds unique index on `SpaceId` |
| `Jobuler.Application/Persistence/AppDbContext.cs` | Added `DbSet<SpaceHomeLeaveConfig> SpaceHomeLeaveConfigs` property in the Spaces section |

## Key decisions

- **Int conversion for enums** — The design SQL schema stores `mode` and `pre_freeze_mode` as `INT`, unlike the group-level `HomeLeaveConfig` which uses string conversion. This aligns with the space-level schema definition and is more storage-efficient.
- **Unique index on `space_id`** — Enforces the one-to-one relationship between Space and its home-leave config at the database level.
- **Follows existing conventions** — Same pattern as `HomeLeaveConfigConfiguration` (explicit column name mapping, key configuration, index) but adapted for space-level usage.

## How it connects

- The `SpaceHomeLeaveConfig` domain entity (created in step 526) is now persistable via EF Core.
- The migration (task 2.5) will generate the actual table creation DDL from this configuration.
- Application-layer commands (`UpdateSpaceHomeLeaveConfigCommand`) and queries (`GetSpaceHomeLeaveConfigQuery`) will use the `SpaceHomeLeaveConfigs` DbSet.
- The solver payload normalizer will read from this DbSet to override group-level home-leave settings.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build should succeed with no errors related to the new configuration.

## What comes next

- Task 2.2: Update `Space` EF configuration for `DeletedAt` and `ManagementTimeoutMinutes`
- Task 2.3: Update `Group` EF configuration for `DeletedBySpaceDeletion`
- Task 2.4: Update `SpaceMembership` EF configuration for `PermissionLevel`
- Task 2.5: Generate EF migration for all schema changes

## Git commit

```bash
git add -A && git commit -m "feat(space-management): add EF configuration for SpaceHomeLeaveConfig"
```
