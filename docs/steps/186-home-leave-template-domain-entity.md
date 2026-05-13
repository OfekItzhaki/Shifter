# 186 — Home-Leave Template Domain Entity

## Phase

Home-Leave Scheduling — Database & Domain Layer

## Purpose

Creates the `HomeLeaveTemplate` domain entity that represents a reusable home-leave configuration template scoped to a space. Admins can save and load these templates to quickly apply standard leave configurations to closed-base groups.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Groups/HomeLeaveTemplate.cs` | Domain entity extending `Entity` and implementing `ITenantScoped`. Includes properties for SpaceId, Name, MinRestHours, EligibilityThresholdHours, LeaveCapacity, LeaveDurationHours. Uses private constructor, static `Create` factory method, and name validation (1–100 chars, no leading/trailing whitespace). |

## Key decisions

- Extends `Entity` (not `AuditableEntity`) because templates only track `created_at`, not `updated_at` — templates are immutable once created (delete and recreate to "update").
- Name validation is enforced at the domain level via `ValidateName()` — rejects empty/whitespace-only names, names over 100 chars, and names with leading/trailing whitespace.
- Follows the same private-constructor + static-factory pattern used by `Group`, `ConstraintRule`, and other domain entities.

## How it connects

- Referenced by `HomeLeaveTemplateConfiguration.cs` (EF Core Fluent API mapping — task 1.5)
- Used by template CRUD commands in the Application layer (task 4.1)
- Persisted in the `home_leave_templates` table created in migration `042_home_leave.sql` (task 1.1)
- Implements `ITenantScoped` for RLS/tenant isolation enforcement

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build
```

Build should succeed with zero errors.

## What comes next

- Task 1.5: EF Core configuration for `HomeLeaveTemplate` (Fluent API mapping, unique index on space_id + name)
- Task 4.1: Template CRUD commands and queries in the Application layer

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add HomeLeaveTemplate domain entity"
```
