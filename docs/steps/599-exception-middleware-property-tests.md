# 599 — Exception Handling Middleware Property Tests

## Phase

Phase: Problem Details Migration — Testing

## Purpose

Validates the five correctness properties defined in the design document for the `ExceptionHandlingMiddleware` using FsCheck property-based testing. These tests ensure universal guarantees hold across all valid inputs, complementing the example-based unit tests.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Middleware/ExceptionHandlingMiddlewarePropertyTests.cs` | 5 property-based tests covering structural completeness, mapping correctness, validation round-trip, rate limit preservation, and production safety |
| `apps/api/Jobuler.Api/Middleware/ExceptionHandlingMiddleware.cs` | Fixed `WriteAsJsonAsync` call to pass `contentType: "application/problem+json"` so the Content-Type header is correctly set per RFC 7807 |

## Key decisions

- Used FsCheck 2.16.6 LINQ query syntax (`from ... in Gen.X() select ...`) for generator composition, matching existing project patterns.
- Generated random exceptions from all 9 mapped types plus 2 unmapped types (NotImplementedException, TimeoutException) for Property 1.
- Property 2 uses a tuple generator that pairs each exception with its expected status/title/detail, distinguishing hardcoded vs dynamic messages.
- Property 3 generates random validation failure lists (1–10 items) with random property names and messages, then verifies the grouping is faithful.
- Property 4 generates random positive integers for RetryAfterSeconds and verifies both the HTTP header and JSON extension.
- Property 5 forces stack traces by throwing/catching, then verifies no sensitive data leaks in production mode.
- Fixed a bug where `WriteAsJsonAsync` was overriding the `Content-Type` header — now passes the content type explicitly.

## How it connects

- These tests validate the middleware refactored in steps 594–596.
- They complement the unit tests from step 598.
- All 5 properties map directly to the design document's correctness properties section.

## How to run / verify

```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~ExceptionHandlingMiddlewarePropertyTests"
```

All 5 property tests should pass (100 iterations each).

## What comes next

- Task 7: Final checkpoint — ensure all tests pass across the entire test suite.

## Git commit

```bash
git add -A && git commit -m "feat(problem-details): property-based tests for exception middleware (5 properties)"
```
