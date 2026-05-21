# 473 — PushoverNotifier Unit Tests

## Phase

Health Check Alerts — Testing

## Purpose

Validates the `PushoverNotifier` implementation with unit tests covering request body format, message content, graceful degradation when credentials are missing, and error logging when the Pushover API returns non-success status codes or throws exceptions.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/HealthChecks/PushoverNotifierTests.cs` | 7 unit tests covering all PushoverNotifier behaviors |

## Key decisions

- Used a custom `MockHttpMessageHandler` inner class to capture HTTP requests and simulate responses/failures, rather than adding a third-party mocking library for HttpClient.
- Verified logger calls using NSubstitute's `ReceivedWithAnyArgs()` pattern, which works with `ILogger<T>` extension methods.
- Tested URL-encoded form body content by reading the raw string from the captured request content.

## How it connects

- Tests validate the `PushoverNotifier` implementation from step 468.
- Covers requirements 5.1 (POST to Pushover API), 5.2 (priority=1), 5.3 (service name + timestamp in message), 5.6 (error logging on failure), 5.7 (graceful degradation when credentials missing).

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~Jobuler.Tests.HealthChecks.PushoverNotifierTests"
```

All 7 tests should pass.

## What comes next

- Property tests for HealthCheckMonitorService state machine (task 6.2)
- Integration tests for health endpoints (task 7.3)

## Git commit

```bash
git add -A && git commit -m "test(health-checks): add PushoverNotifier unit tests"
```
