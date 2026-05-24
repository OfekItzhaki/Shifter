# 527 — Group `DeletedBySpaceDeletion` EF Configuration

## Phase

Space Management — Infrastructure Layer

## Purpose

Maps the `DeletedBySpaceDeletion` domain property on the `Group` entity to its corresponding PostgreSQL column, enabling EF Core to persist and query the cascade-delete tracking flag used during space soft-delete/restore operations.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs` | Added `builder.Property(g => g.DeletedBySpaceDeletion).HasColumnName("deleted_by_space_deletion").HasDefaultValue(false)` |

## Key decisions

- Column name follows existing snake_case convention (`deleted_by_space_deletion`)
- Default value is `false` — groups are not cascade-deleted by default
- Placed immediately after the `deleted_at` mapping for logical grouping

## How it connects

- **Domain (task 1.2)**: The `Group` entity already has the `DeletedBySpaceDeletion` property with `SoftDeleteBySpace()` and `RestoreFromSpaceDeletion()` methods
- **Migration (task 2.5)**: The upcoming migration will generate the `ALTER TABLE groups ADD COLUMN deleted_by_space_deletion BOOLEAN NOT NULL DEFAULT FALSE` DDL from this configuration
- **Application (tasks 5.1, 5.2)**: The `SoftDeleteSpaceCommand` and `RestoreSpaceCommand` handlers will read/write this flag to distinguish cascade-deleted groups from individually-deleted ones

## How to run / verify

```bash
cd apps/api/Jobuler.Infrastructure
dotnet build --no-restore
```

Build should succeed with no new errors.

## What comes next

- Task 2.4: Update `SpaceMembership` EF configuration for `PermissionLevel`
- Task 2.5: Generate the EF migration covering all schema changes

## Git commit

```bash
git add -A && git commit -m "feat(space-management): map Group.DeletedBySpaceDeletion to EF configuration"
```
