# 430 — LemonSqueezy DI Registration

## Phase
LemonSqueezy Billing Integration — Infrastructure Layer

## Purpose
Wire up all LemonSqueezy billing services in the dependency injection container so the application can resolve `ILemonSqueezyClient`, `IWebhookSignatureValidator`, and `LemonSqueezySettings` at runtime. Includes startup validation to fail fast if required configuration values are missing.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Api/Program.cs` | Added `using` for `Jobuler.Application.Billing` and `Jobuler.Infrastructure.Billing`. Registered `LemonSqueezySettings` from the `"LemonSqueezy"` configuration section via `Configure<T>`. Added startup validation call. Registered `ILemonSqueezyClient` → `LemonSqueezyClient` as a typed `HttpClient` (scoped). Registered `IWebhookSignatureValidator` → `WebhookSignatureValidator` as singleton. |

## Key decisions

- **Startup validation**: The `LemonSqueezySettings.Validate()` method is called eagerly during startup (not deferred to first use). This ensures the app fails fast with a clear error message if any required config value is missing.
- **HttpClient via IHttpClientFactory**: `AddHttpClient<ILemonSqueezyClient, LemonSqueezyClient>` uses the built-in `IHttpClientFactory` pattern, which manages `HttpClient` lifetimes correctly (avoids socket exhaustion) and makes the service scoped.
- **Singleton for WebhookSignatureValidator**: The validator only depends on the webhook secret (read once from `IOptions<LemonSqueezySettings>`), so it's safe as a singleton — no per-request state.
- **Base address on HttpClient**: Set to `https://api.lemonsqueezy.com/` with a 30-second timeout, matching the LemonSqueezy API base URL.

## How it connects

- Depends on: `LemonSqueezySettings` (task 2.1), `LemonSqueezyClient` (task 2.2), `WebhookSignatureValidator` (task 2.3)
- Used by: `CreateCheckoutCommand` handler, `LemonSqueezyWebhookController`, and all webhook event handlers that need `ILemonSqueezyClient` or `IWebhookSignatureValidator`

## How to run / verify

```bash
cd apps/api
dotnet build
```

The build succeeds with 0 errors and 0 warnings. At runtime, the app will throw `InvalidOperationException` if the `LemonSqueezy` configuration section is missing or has empty values.

## What comes next

- Task 3 (Checkpoint): Verify infrastructure compiles and configuration validation works
- Task 4: Application layer checkout command (uses `ILemonSqueezyClient`)
- Task 7: Webhook controller (uses `IWebhookSignatureValidator`)

## Git commit

```bash
git add -A && git commit -m "feat(billing): register LemonSqueezy services in DI container"
```
