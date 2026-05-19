# 439 — LemonSqueezy Configuration in appsettings

## Phase

LemonSqueezy Billing Integration — Integration wiring and cleanup

## Purpose

Adds the `"LemonSqueezy"` configuration section to `appsettings.json` and `appsettings.Development.json` so the DI registration (task 2.5) can bind `LemonSqueezySettings` from configuration at runtime. Also documents the required environment variables in `.env.example` and wires them through `docker-compose.yml`.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Api/appsettings.json` | Added `"LemonSqueezy"` section with empty placeholder values for `ApiKey`, `WebhookSecret`, `StoreId`, `DefaultVariantId`, `TestVariantId` |
| `Jobuler.Api/appsettings.Development.json` | Added `"LemonSqueezy"` section with descriptive placeholder values indicating env var override names |
| `infra/compose/.env.example` | Added `LEMONSQUEEZY_*` environment variables with documentation comments |
| `infra/compose/docker-compose.yml` | Added `LemonSqueezy__*` env var mappings to the API service |

## Key decisions

- **Empty values in appsettings.json**: Production values come from environment variables or secrets manager, never from committed config files. Empty strings ensure the startup validation (from `LemonSqueezySettings.Validate()`) will fail fast if env vars aren't set.
- **Descriptive placeholders in Development.json**: Values like `SET_VIA_ENV_VAR_LemonSqueezy__ApiKey` make it obvious to developers that they need to set environment variables locally, rather than using these placeholder strings.
- **Docker-compose env var mapping**: Uses the .NET `__` (double underscore) separator convention to map flat env vars to nested configuration sections (e.g., `LemonSqueezy__ApiKey` → `LemonSqueezy:ApiKey`).
- **No secrets committed**: Per security rules, all sensitive values (API key, webhook secret) are empty in source and must be provided via environment.

## How it connects

- **Consumed by**: `LemonSqueezySettings` class (task 2.1) which is bound from the `"LemonSqueezy"` config section
- **Registered in**: `Program.cs` DI wiring (task 2.5) via `Configure<LemonSqueezySettings>(config.GetSection("LemonSqueezy"))`
- **Validated at**: Application startup — empty values trigger `InvalidOperationException` with specific missing key names
- **Used by**: `LemonSqueezyClient`, `WebhookSignatureValidator`, `CreateCheckoutCommand`, test-charge endpoint

## How to run / verify

1. Build the solution: `dotnet build` — should succeed with 0 errors
2. Run without env vars set: app should fail to start with `LemonSqueezy configuration is incomplete. Missing or empty values: ApiKey, WebhookSecret, StoreId, DefaultVariantId, TestVariantId`
3. Set env vars and run: app should start normally

## What comes next

- Task 9.3: Unit tests for status mapping and configuration validation

## Git commit

```bash
git add -A && git commit -m "feat(billing): add LemonSqueezy configuration to appsettings and env vars"
```
