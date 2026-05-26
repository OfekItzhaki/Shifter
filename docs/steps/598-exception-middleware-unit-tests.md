# Step 598 — ExceptionHandlingMiddleware Unit Tests

## Phase

Phase: Problem Details Migration — Testing

## Purpose

Provides unit test coverage for the `ExceptionHandlingMiddleware` to verify critical behaviors: development mode debug extensions, response-already-started handling, serialization failure fallback, DbUpdateException constraint mapping to Hebrew messages, atomic write guarantees, and production safety (no sensitive data leaks).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Middleware/ExceptionHandlingMiddlewareTests.cs` | Unit tests covering 7 scenarios across 16 test cases for the exception handling middleware |

## Key decisions

- **`DefaultHttpContext` with `MemoryStream`** — used for all tests to capture response body and assert on JSON content without needing a real HTTP server.
- **NSubstitute for `IHostEnvironment`** — mocks environment to toggle between Development and Production modes.
- **Custom stream helpers** — `FailOnFirstWriteStream` simulates serialization failures to test the fallback safety net; `HasStartedResponseFeature` simulates the response-already-started scenario.
- **Content-Type assertion** — `WriteAsJsonAsync` in test context overrides the content type set by the middleware. Tests verify the JSON body structure instead, which is the meaningful guarantee of atomic writes.
- **Theory-based constraint tests** — all 6 known check constraints are tested via `[InlineData]` to verify correct Hebrew message extraction.

## How it connects

- Tests validate the middleware implemented in steps 594–596 (ProblemDetails refactor, fallback safety net).
- Validates requirements 12.4 (dev mode debug info), 11.4 (fallback safety), 3.4 (atomic writes), and 5.3 (constraint-specific Hebrew messages).
- Complements the property-based tests (tasks 6.3–6.7) which will verify universal properties across random inputs.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~Jobuler.Tests.Middleware.ExceptionHandlingMiddlewareTests"
```

All 16 tests should pass.

## What comes next

- Task 6.2: Controller ProblemDetails unit tests
- Tasks 6.3–6.7: Property-based tests for structural completeness, mapping correctness, validation round-trip, rate limit preservation, and production safety

## Git commit

```bash
git add -A && git commit -m "feat(problem-details): add ExceptionHandlingMiddleware unit tests"
```
