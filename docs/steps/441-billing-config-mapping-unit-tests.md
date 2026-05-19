# 441 — Billing Configuration & Mapping Unit Tests

## Phase

LemonSqueezy Billing Integration — Testing

## Purpose

Validates the correctness of LemonSqueezy status mapping, configuration validation, checkout metadata, test-charge metadata, webhook endpoint attributes, and permission enforcement through unit tests and a property-based test.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Tests/Billing/BillingConfigAndMappingTests.cs` | 27 unit/property tests covering Property 16 (configuration validation), status mapping, metadata correctness, endpoint attributes, and permission checks |

## Key decisions

- Used reflection to access the private `StatusMapping` dictionary on `HandleSubscriptionUpdatedCommandHandler` to verify all 5 mappings without exposing internals
- Used `System.Reflection` to verify `[AllowAnonymous]`, `[Authorize]`, `[HttpPost]`, and `[Route]` attributes on controllers/endpoints
- Property-based test (FsCheck) verifies that whitespace-only values are rejected by `LemonSqueezySettings.Validate()`
- Permission enforcement tested by mocking `IPermissionService` to throw `UnauthorizedAccessException`

## How it connects

- Validates Requirements 4.2 (status mapping), 8.4 (test-charge metadata), 9.4 (configuration validation)
- Tests the infrastructure created in steps 428 (LemonSqueezySettings), 434 (HandleSubscriptionUpdatedCommand), 437 (WebhookController), 438 (test-charge endpoint)

## How to run / verify

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~Jobuler.Tests.Billing.BillingConfigAndMappingTests"
```

All 27 tests should pass.

## What comes next

Final checkpoint (task 10) — ensure all tests pass and integration is complete.

## Git commit

```bash
git add -A && git commit -m "feat(billing): unit tests for status mapping and configuration validation"
```
