# Step 154 — WebPush NuGet Package and VAPID Configuration

## Phase

Push Notifications — Backend Foundation

## Purpose

Add the WebPush library for VAPID-based push notification delivery and create the configuration class that loads VAPID keys from environment variables. This is a prerequisite for the PushNotificationSender that will encrypt and dispatch push messages.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Jobuler.Infrastructure.csproj` | Added `WebPush` NuGet package (v1.0.12) for VAPID signing and RFC 8291 payload encryption |
| `apps/api/Jobuler.Infrastructure/Notifications/VapidSettings.cs` | POCO options class with PublicKey, PrivateKey, and Subject properties |
| `apps/api/Jobuler.Api/Program.cs` | Registered `VapidSettings` in DI via `Configure<VapidSettings>`, loading from configuration/environment variables |

## Key decisions

- **WebPush package**: Chose the `WebPush` (web-push-csharp) NuGet package — it's the most popular .NET library for Web Push and handles both VAPID JWT signing (RFC 8292) and payload encryption (RFC 8291).
- **Environment variable loading**: VAPID keys are loaded from both `IConfiguration` (appsettings) and direct environment variables as fallback. This supports both Docker/container deployments (env vars) and local development (user-secrets or appsettings).
- **No secrets in source**: Per security rules, VAPID private key is never committed. Only environment variable names are referenced.
- **Options pattern**: Used `Configure<VapidSettings>` so downstream services can inject `IOptions<VapidSettings>` following the standard ASP.NET Core options pattern.

## How it connects

- `VapidSettings` will be injected into `PushNotificationSender` (task 3.2) via `IOptions<VapidSettings>`
- The WebPush package provides `WebPushClient` used by `PushNotificationSender` to encrypt payloads and sign requests
- The VAPID public key is also exposed to the frontend via `NEXT_PUBLIC_VAPID_PUBLIC_KEY` (task 9.1)

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
```

Build should succeed with no errors. The WebPush package is restored and VapidSettings is registered in DI.

## What comes next

- Task 2.1: CreatePushSubscriptionCommand (uses the persisted subscription data)
- Task 3.2: PushNotificationSender implementation (injects VapidSettings and uses WebPush library)
- Task 3.4: Register PushNotificationSender in DI

## Git commit

```bash
git add -A && git commit -m "feat(push): add WebPush NuGet package and VAPID configuration"
```
