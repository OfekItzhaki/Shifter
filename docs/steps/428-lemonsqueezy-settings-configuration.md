# 428 — LemonSqueezy Settings Configuration

## Phase

LemonSqueezy Billing Integration — Infrastructure Layer

## Purpose

Provides a strongly-typed configuration class for all LemonSqueezy billing credentials and identifiers. Includes startup validation that prevents the application from running with incomplete billing configuration, identifying exactly which values are missing.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Infrastructure/Billing/LemonSqueezySettings.cs` | Configuration POCO with `ApiKey`, `WebhookSecret`, `StoreId`, `DefaultVariantId`, `TestVariantId` properties and a `Validate()` method that throws if any required value is missing or whitespace-only |

## Key decisions

- **All properties required**: Unlike VAPID settings which degrade gracefully, billing configuration is mandatory — the app cannot function without it, so validation is strict.
- **Validate method on the class itself**: Keeps validation logic co-located with the settings rather than spreading it across DI registration. The DI layer (task 2.5) will call `Validate()` at startup.
- **Specific error messages**: The exception message lists all missing keys by name so operators can fix configuration issues without guessing.
- **No secrets in source**: All values come from environment configuration per security rules.

## How it connects

- **Consumed by**: `LemonSqueezyClient` (HTTP client, task 2.2), `WebhookSignatureValidator` (task 2.3), DI registration (task 2.5)
- **Registered from**: `appsettings.json` section `"LemonSqueezy"` (task 9.2)
- **Validated at**: Application startup via DI wiring (task 2.5)

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Infrastructure/Jobuler.Infrastructure.csproj
```

The class compiles cleanly. Full integration verification happens in task 2.5 when DI registration calls `Validate()`.

## What comes next

- Task 2.2: `ILemonSqueezyClient` HTTP client (uses `ApiKey` and `StoreId`)
- Task 2.3: `IWebhookSignatureValidator` (uses `WebhookSecret`)
- Task 2.5: DI registration that binds configuration and calls `Validate()`

## Git commit

```bash
git add -A && git commit -m "feat(billing): add LemonSqueezySettings configuration class with startup validation"
```
