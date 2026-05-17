# 307 — Platform Settings Domain Entity

## Phase
Admin Session Timeout — Domain Layer

## Purpose
Introduces the `PlatformSettings` domain entity for storing system-level key-value configuration (e.g., super-admin session timeout). This entity is not tenant-scoped — it holds platform-wide settings shared across all spaces.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Platform/PlatformSettings.cs` | Domain entity with `Key`/`Value` properties, `Create` factory, and `UpdateValue` method |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Added `DbSet<PlatformSettings>` registration |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/PlatformSettingsConfiguration.cs` | EF Core Fluent API configuration mapping to `platform_settings` table |

## Key decisions
- Entity extends `AuditableEntity` (provides `Id`, `CreatedAt`, `UpdatedAt`) but does NOT implement `ITenantScoped` since platform settings are global.
- Private parameterless constructor prevents direct instantiation; `Create` factory enforces controlled creation.
- `UpdateValue` calls `Touch()` to update the `UpdatedAt` timestamp.
- EF configuration maps to the existing `platform_settings` table (created in migration 061) with a unique index on `key`.

## How it connects
- The `platform_settings` table was already created in `infra/migrations/061_platform_settings.sql`.
- Subsequent tasks (3.3 `UpdatePlatformSettingsCommand`, 4.4 platform settings endpoints) will use this entity to read/write the `platform_timeout_minutes` setting.

## How to run / verify
```bash
cd apps/api
dotnet build
```
Build should succeed with zero errors.

## What comes next
- Task 2.1: `ReAuthenticateCommand` and handler
- Task 3.3: `UpdatePlatformSettingsCommand` that loads and updates `PlatformSettings` by key

## Git commit
```bash
git add -A && git commit -m "feat(admin-session-timeout): add PlatformSettings domain entity and EF Core configuration"
```
