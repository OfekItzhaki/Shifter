# 217 — Color-Coded Roles Backend

## Phase

Feature — Color-Coded Roles

## Purpose

Adds an optional color property to the SpaceRole entity so admins can assign a hex color to roles for visual distinction in schedule views and member lists. This step covers the full backend: domain, infrastructure (migration + EF Core), application (commands, validation), and API layer.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Spaces/SpaceRole.cs` | Added `Color` property; updated `Create`, `CreateForGroup`, and `Update` methods to accept optional color |
| `infra/migrations/044_space_role_color.sql` | Added nullable `color` TEXT column with CHECK constraint for hex format |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/SpaceConfiguration.cs` | Added EF Core mapping for `Color` column |
| `apps/api/Jobuler.Application/Groups/Commands/GroupRoleCommands.cs` | Added `Color` parameter to `CreateGroupRoleCommand`, `UpdateGroupRoleCommand`, and `GroupRoleDto`; updated handlers to pass color through |
| `apps/api/Jobuler.Application/Groups/Queries/GetGroupRolesQuery.cs` | Updated projection to include `Color` in the DTO |
| `apps/api/Jobuler.Application/Groups/Validators/GroupRoleCommandValidators.cs` | New file — FluentValidation rules for both create/update commands (hex color regex) |
| `apps/api/Jobuler.Api/Controllers/GroupRolesController.cs` | Added `Color` to `GroupRoleRequest`; passed color to commands in Create and Update actions |

## Key decisions

- Color is optional (nullable) — roles without a color render with default styling
- Database CHECK constraint (`^#[0-9a-fA-F]{6}$`) acts as a safety net alongside application-level FluentValidation
- The `Color` parameter is added as the last optional parameter in all methods to maintain backward compatibility with existing callers
- Validators are in a separate file following the existing `Groups/Validators/` pattern

## How it connects

- Frontend will read `color` from `GroupRoleDto` in GET responses
- Frontend will send `color` in create/update requests via the `GroupRoleRequest` body
- The color picker component (next step) will only offer valid preset hex values

## How to run / verify

```bash
# Build
cd apps/api && dotnet build

# Run existing tests (all 12 pass)
dotnet test --filter "GroupRoleCrudTests"

# Run migration against local DB
psql -f infra/migrations/044_space_role_color.sql
```

## What comes next

- Frontend: Update API client DTO, create RoleColorPicker component, integrate into role forms
- Frontend: Render color indicators in schedule views and member list

## Git commit

```bash
git add -A && git commit -m "feat(color-roles): add color property to SpaceRole backend (domain, migration, commands, API)"
```
