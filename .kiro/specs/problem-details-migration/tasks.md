# Implementation Plan: Problem Details Migration

## Overview

Migrate all API error responses from the custom `{ error: "...", errors: [...] }` format to RFC 7807 ProblemDetails (`application/problem+json`). The implementation proceeds in layers: first the internal factory and safety guard, then the middleware refactoring, then controller-level migrations, and finally comprehensive testing.

## Tasks

- [x] 1. Create ProblemDetailsFactory and supporting infrastructure
  - [x] 1.1 Create `ProblemDetailsFactory` static helper class
    - Create file `apps/api/Jobuler.Api/Middleware/ProblemDetailsFactory.cs`
    - Implement `Create(HttpContext context, int statusCode, string title, string detail, string typeSlug, IDictionary<string, object?>? extensions = null)` method
    - Set `Type` to `https://docs.jobuler.com/errors/{typeSlug}`
    - Set `Instance` to `context.Request.Path`
    - Set `Status` to `statusCode`
    - Always add `traceId` extension from `context.TraceIdentifier`
    - Merge any additional extensions passed in
    - Use ASP.NET Core's built-in `Microsoft.AspNetCore.Mvc.ProblemDetails` class
    - _Requirements: 1.1, 1.3, 1.4, 1.5_

  - [x] 1.2 Create `ProductionSafetyGuard` static helper class
    - Create file `apps/api/Jobuler.Api/Middleware/ProductionSafetyGuard.cs`
    - Implement `Sanitize(ProblemDetails problem, IHostEnvironment env)` method
    - In production: strip `exceptionType`, `stackTrace`, and `innerException` extension keys if present
    - In development: leave all extensions intact for debugging
    - Enforce stripping regardless of any other configuration settings
    - _Requirements: 12.1, 12.2, 12.3, 12.5_

  - [x] 1.3 Create `ProblemDetailsResults` static helper for controllers
    - Create file `apps/api/Jobuler.Api/Middleware/ProblemDetailsResults.cs`
    - Implement `Problem(HttpContext context, int statusCode, string title, string detail, string typeSlug, IDictionary<string, object?>? extensions = null)` returning `ObjectResult`
    - Set `Content-Type` to `application/problem+json` on the result
    - Internally use `ProblemDetailsFactory.Create()` for consistency
    - _Requirements: 9.4, 1.1, 1.2, 1.3_

- [x] 2. Refactor ExceptionHandlingMiddleware to use ProblemDetails
  - [x] 2.1 Refactor `ExceptionHandlingMiddleware` to inject `IHostEnvironment`
    - Modify constructor to accept `IHostEnvironment` parameter
    - Store as `_environment` field for production/development checks
    - Update DI registration in `Program.cs` if needed (ASP.NET Core injects middleware dependencies automatically)
    - _Requirements: 12.4, 12.5_

  - [x] 2.2 Implement ProblemDetails response writing in the middleware
    - Replace all `JsonSerializer.Serialize(new { error = ... })` calls with `ProblemDetailsFactory.Create()` usage
    - Set `Content-Type` to `application/problem+json` for all error responses
    - Write response atomically using a single `WriteAsJsonAsync` call with `JsonSerializerOptions`
    - Call `ProductionSafetyGuard.Sanitize()` as the final step before serialization
    - In development mode, optionally add `exceptionType`, `stackTrace`, `innerException` extensions before sanitization
    - _Requirements: 1.1, 1.2, 3.4, 8.4, 12.4_

  - [x] 2.3 Implement ValidationException mapping with field-level errors
    - Map `ValidationException` to status 400 with title "Validation Failed"
    - Set `detail` to "אימות הנתונים נכשל."
    - Set `type` to `https://docs.jobuler.com/errors/validation-failed`
    - Group `ve.Errors` by `PropertyName` into a dictionary and add as `errors` extension property
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 2.4 Implement all other exception type mappings
    - `UnauthorizedAccessException` → 403, "Forbidden", "אין לך הרשאה לבצע פעולה זו.", `forbidden`
    - `KeyNotFoundException` → 404, "Not Found", "הפריט המבוקש לא נמצא.", `not-found`
    - `ConflictException` → 409, "Conflict", exception message, `conflict`
    - `DomainValidationException` → 422, "Unprocessable Entity", exception message, `unprocessable-entity`
    - `InvalidOperationException` → 400, "Bad Request", exception message, `bad-request`
    - `ArgumentException` → 400, "Bad Request", exception message, `bad-request`
    - `PaymentRequiredException` → 402, "Payment Required", exception message, `payment-required`
    - Unhandled exceptions → 500, "Internal Server Error", "אירעה שגיאה בלתי צפויה. נסה שוב מאוחר יותר.", `internal-server-error`
    - _Requirements: 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 5.1, 5.2, 5.4, 6.1, 6.2, 6.3, 8.1, 8.2, 8.3, 10.1, 10.2, 10.3, 11.1, 11.2, 11.3, 13.1, 13.2, 13.3_

  - [x] 2.5 Implement RateLimitExceededException mapping with Retry-After
    - Map to status 429 with title "Too Many Requests"
    - Set `detail` to "Rate limit exceeded. Try again later."
    - Set `Retry-After` response header to `RetryAfterSeconds` value
    - Add `retryAfterSeconds` extension property with the numeric value
    - Set `type` to `https://docs.jobuler.com/errors/rate-limit-exceeded`
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 2.6 Implement DbUpdateException mapping (unique/check constraints)
    - Unique constraint violations (23505, "duplicate key", "unique") → 409, "Conflict", localized message, `conflict`
    - Check constraint violations (23514, "violates check constraint") → 409, "Conflict", constraint-specific Hebrew message, `conflict`
    - Preserve existing `ExtractCheckConstraintMessage` logic
    - Other DbUpdateException → 500, "Internal Server Error", generic Hebrew message
    - _Requirements: 5.3, 5.4, 13.1_

  - [x] 2.7 Implement fallback safety net for serialization failures
    - Wrap the ProblemDetails creation and serialization in a try-catch
    - On inner exception: log critical, preserve original status code, write minimal JSON `{"type":"about:blank","title":"Error","status":<code>}`
    - Handle `Response.HasStarted` scenario — log error, do not attempt to write
    - _Requirements: 11.4_

- [x] 3. Checkpoint - Verify middleware compiles and basic behavior
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Migrate controller-level error responses to ProblemDetails
  - [x] 4.1 Migrate `ShiftRequestsController` error responses
    - Replace `UnprocessableEntity(new ShiftRequestErrorResponse(...))` with `ProblemDetailsResults.Problem()` call
    - Use status 422, title "Unprocessable Entity", detail from `result.RejectionReason`
    - Add `alternativeSlots` extension property when `result.AlternativeSlots` is not null
    - Use type slug `shift-request-rejected`
    - Migrate `Cancel` endpoint error response similarly (422, detail from `result.ErrorMessage`)
    - Remove or deprecate `ShiftRequestErrorResponse` record
    - _Requirements: 9.1, 9.2, 9.4_

  - [x] 4.2 Migrate `WaitlistController` error responses
    - Replace `UnprocessableEntity(new WaitlistErrorResponse(...))` with `ProblemDetailsResults.Problem()` call
    - Use status 422, title "Unprocessable Entity", detail from `result.ErrorMessage`
    - Use type slug `waitlist-rejected`
    - Migrate both `Join` and `AcceptOffer` error responses
    - Remove or deprecate `WaitlistErrorResponse` record
    - _Requirements: 9.3, 9.4_

- [x] 5. Checkpoint - Verify full compilation and existing tests
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Write unit tests for middleware and controllers
  - [x] 6.1 Create unit tests for `ExceptionHandlingMiddleware`
    - Create file `apps/api/Jobuler.Tests/Middleware/ExceptionHandlingMiddlewareTests.cs`
    - Use `DefaultHttpContext` with `MemoryStream` response body for assertions
    - Test development mode includes `exceptionType`, `stackTrace`, `innerException` extensions
    - Test `Response.HasStarted` scenario — no write attempted
    - Test serialization failure triggers minimal JSON fallback
    - Test `DbUpdateException` with specific constraint names produces correct Hebrew messages
    - Test atomic write — single response operation (no partial writes)
    - _Requirements: 12.4, 11.4, 3.4, 5.3_

  - [x] 6.2 Create unit tests for controller ProblemDetails responses
    - Create file `apps/api/Jobuler.Tests/Middleware/ControllerProblemDetailsTests.cs`
    - Test `ShiftRequestsController` rejection includes `alternativeSlots` extension
    - Test `WaitlistController` rejection returns ProblemDetails with 422 and correct detail
    - Use NSubstitute mocks for services
    - _Requirements: 9.1, 9.2, 9.3_

  - [x]* 6.3 Write property test: Structural Completeness (Property 1)
    - Create file `apps/api/Jobuler.Tests/Middleware/ExceptionHandlingMiddlewarePropertyTests.cs`
    - **Property 1: Structural Completeness**
    - Generate random exceptions from all mapped types + random unmapped exceptions
    - Generate random request paths and TraceIdentifiers
    - Assert response always contains `type`, `title`, `status`, `detail`, `instance`, and `traceId`
    - Assert `Content-Type` is `application/problem+json`
    - Assert `type` matches pattern `https://docs.jobuler.com/errors/{slug}`
    - Assert `instance` matches request path
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5**

  - [x]* 6.4 Write property test: Exception-to-Response Mapping Correctness (Property 2)
    - Add to `ExceptionHandlingMiddlewarePropertyTests.cs`
    - **Property 2: Exception-to-Response Mapping Correctness**
    - Generate random exceptions of each mapped type with random messages
    - Verify status code, title, and detail match the mapping table exactly
    - For hardcoded messages: assert exact match
    - For dynamic messages: assert `detail` equals exception's `Message` property
    - **Validates: Requirements 2.1, 2.2, 2.4, 2.5, 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 5.1, 5.2, 5.4, 6.1, 6.2, 6.3, 7.1, 7.4, 7.5, 8.1, 8.2, 8.3, 10.1, 10.2, 10.3, 11.1, 11.2, 11.3, 13.1, 13.2, 13.3**

  - [x]* 6.5 Write property test: Validation Errors Round-Trip (Property 3)
    - Add to `ExceptionHandlingMiddlewarePropertyTests.cs`
    - **Property 3: Validation Errors Round-Trip**
    - Generate random lists of `ValidationFailure` with random property names and messages
    - Assert `errors` extension dictionary groups messages by property name faithfully
    - No messages lost, no messages added, no messages assigned to wrong property
    - **Validates: Requirements 2.3**

  - [x]* 6.6 Write property test: Rate Limit Extension Data Preservation (Property 4)
    - Add to `ExceptionHandlingMiddlewarePropertyTests.cs`
    - **Property 4: Rate Limit Extension Data Preservation**
    - Generate random positive integers for `RetryAfterSeconds`
    - Assert `Retry-After` header contains the exact value
    - Assert `retryAfterSeconds` extension property contains the exact numeric value
    - **Validates: Requirements 7.2, 7.3**

  - [x]* 6.7 Write property test: Production Safety — No Sensitive Data Leaks (Property 5)
    - Add to `ExceptionHandlingMiddlewarePropertyTests.cs`
    - **Property 5: Production Safety — No Sensitive Data Leaks**
    - Generate random exceptions with stack traces, inner exceptions, and type names
    - Run middleware in production mode
    - Assert serialized response body never contains stack trace text, exception type name, or inner exception message
    - **Validates: Requirements 8.4, 12.1, 12.2, 12.3, 12.5**

- [x] 7. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The implementation uses C# with ASP.NET Core's built-in `ProblemDetails` class
- FsCheck 2.16.6 with FsCheck.Xunit is already available in the test project
- Step documentation under `docs/steps/` should be created alongside each implementation step per workspace rules

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2"] },
    { "id": 1, "tasks": ["1.3", "2.1"] },
    { "id": 2, "tasks": ["2.2", "2.3", "2.4", "2.5", "2.6"] },
    { "id": 3, "tasks": ["2.7"] },
    { "id": 4, "tasks": ["4.1", "4.2"] },
    { "id": 5, "tasks": ["6.1", "6.2"] },
    { "id": 6, "tasks": ["6.3", "6.4", "6.5", "6.6", "6.7"] }
  ]
}
```
