# Step 598 — Controller ProblemDetails Unit Tests

## Phase

Phase: Problem Details Migration — Testing

## Purpose

Validates that controllers (`ShiftRequestsController`, `WaitlistController`) return RFC 7807 ProblemDetails responses with correct status codes, detail messages, and extension properties when rejecting requests. Ensures the `alternativeSlots` extension is present only when alternative slots are available.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Tests/Middleware/ControllerProblemDetailsTests.cs` | Unit tests for controller-level ProblemDetails responses using NSubstitute mocks |

## Key decisions

- Used in-memory EF Core database to seed a `Person` entity so `ResolvePersonIdAsync` resolves correctly during tests.
- Mocked `IShiftRequestService` and `IWaitlistService` with NSubstitute to control rejection results.
- Asserted on `ObjectResult.Value` cast to `ProblemDetails` to verify the response shape without needing HTTP integration.
- Verified `alternativeSlots` extension is absent when `AlternativeSlots` is null (Req 9.2 conditional behavior).

## How it connects

- Tests validate the controller migrations done in tasks 4.1 and 4.2.
- Uses `ProblemDetailsResults.Problem()` and `ProblemDetailsFactory.Create()` from task 1.3 and 1.1.
- Complements the middleware-level tests in task 6.1.

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~Jobuler.Tests.Middleware.ControllerProblemDetailsTests" --verbosity normal
```

All 5 tests should pass.

## What comes next

- Property-based tests for the middleware (tasks 6.3–6.7).
- Final checkpoint to ensure all tests pass (task 7).

## Git commit

```bash
git add -A && git commit -m "test(problem-details): add controller ProblemDetails unit tests"
```
