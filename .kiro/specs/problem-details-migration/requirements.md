# Requirements Document

## Introduction

Migrate all API error responses from the current custom `{ error: "...", errors: [...] }` format to the RFC 7807 ProblemDetails standard (`application/problem+json`). The migration covers the ExceptionHandlingMiddleware and all controller-level error responses, preserving existing Hebrew error messages and HTTP status codes while adding structured metadata (traceId, type URI, extensions) for improved debugging and frontend consumption.

## Glossary

- **ProblemDetails_Response**: A JSON response body conforming to RFC 7807, containing fields: `type`, `title`, `status`, `detail`, `instance`, and optional extension properties
- **ExceptionHandling_Middleware**: The ASP.NET Core middleware (`ExceptionHandlingMiddleware.cs`) that catches unhandled exceptions and converts them into HTTP error responses
- **Trace_Id**: A correlation identifier (from `HttpContext.TraceIdentifier`) included in every error response to enable log correlation
- **Type_URI**: A URI reference in the `type` field of ProblemDetails that identifies the problem type; uses placeholder documentation URIs until real documentation is available
- **Validation_Errors**: Field-level validation failures produced by FluentValidation, mapped to the `errors` extension property keyed by field name
- **Extension_Property**: Additional JSON properties beyond the standard RFC 7807 fields, used to carry domain-specific data (e.g., `alternativeSlots`, `retryAfterSeconds`)
- **Frontend_Client**: The Next.js application that consumes API responses and must adapt to the new error format

## Requirements

### Requirement 1: ProblemDetails Response Structure

**User Story:** As a frontend developer, I want all API error responses to follow a consistent RFC 7807 structure, so that I can implement a single error-handling path for all error types.

#### Acceptance Criteria

1. WHEN an error response is produced, THE ExceptionHandling_Middleware SHALL return a JSON body containing the fields `type`, `title`, `status`, `detail`, and `instance`
2. WHEN an error response is produced, THE ExceptionHandling_Middleware SHALL set the `Content-Type` header to `application/problem+json`
3. WHEN an error response is produced, THE ExceptionHandling_Middleware SHALL include a `traceId` extension property containing the value of `HttpContext.TraceIdentifier`
4. THE ExceptionHandling_Middleware SHALL set the `type` field to a URI in the format `https://docs.jobuler.com/errors/{error-type-slug}`
5. THE ExceptionHandling_Middleware SHALL set the `instance` field to the request path that triggered the error

### Requirement 2: Validation Error Mapping

**User Story:** As a frontend developer, I want validation errors to include field-level details in a structured format, so that I can display inline validation messages next to form fields.

#### Acceptance Criteria

1. WHEN a FluentValidation `ValidationException` is thrown, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 400
2. WHEN a FluentValidation `ValidationException` is thrown, THE ExceptionHandling_Middleware SHALL set the `detail` field to "אימות הנתונים נכשל."
3. WHEN a FluentValidation `ValidationException` is thrown, THE ExceptionHandling_Middleware SHALL include an `errors` extension property containing a dictionary keyed by property name, where each value is an array of error message strings
4. WHEN a FluentValidation `ValidationException` is thrown, THE ExceptionHandling_Middleware SHALL set the `title` field to "Validation Failed"
5. WHEN a FluentValidation `ValidationException` is thrown, THE ExceptionHandling_Middleware SHALL set the `type` field to `https://docs.jobuler.com/errors/validation-failed`

### Requirement 3: Forbidden Access Error Mapping

**User Story:** As a system operator, I want forbidden access attempts to produce a standard ProblemDetails response, so that monitoring tools can parse error responses uniformly.

#### Acceptance Criteria

1. WHEN an `UnauthorizedAccessException` is thrown, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 403
2. WHEN an `UnauthorizedAccessException` is thrown, THE ExceptionHandling_Middleware SHALL set the `detail` field to "אין לך הרשאה לבצע פעולה זו."
3. WHEN an `UnauthorizedAccessException` is thrown, THE ExceptionHandling_Middleware SHALL set the `title` field to "Forbidden"
4. THE ExceptionHandling_Middleware SHALL generate the HTTP status code and ProblemDetails body atomically as a single response write operation

### Requirement 4: Not Found Error Mapping

**User Story:** As a frontend developer, I want not-found errors to use ProblemDetails format, so that I can distinguish them from other 4xx errors programmatically.

#### Acceptance Criteria

1. WHEN a `KeyNotFoundException` is thrown, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 404
2. WHEN a `KeyNotFoundException` is thrown, THE ExceptionHandling_Middleware SHALL set the `detail` field to "הפריט המבוקש לא נמצא."
3. WHEN a `KeyNotFoundException` is thrown, THE ExceptionHandling_Middleware SHALL set the `title` field to "Not Found"

### Requirement 5: Conflict Error Mapping

**User Story:** As a frontend developer, I want conflict errors (duplicate records) to use ProblemDetails format, so that I can show appropriate retry or rename guidance to users.

#### Acceptance Criteria

1. WHEN a `ConflictException` is thrown, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 409
2. WHEN a `ConflictException` is thrown, THE ExceptionHandling_Middleware SHALL set the `detail` field to the exception message
3. WHEN a database constraint violation (unique or check) is detected, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 409 and `detail` resolved from a configurable message source (localization) rather than hardcoded strings
4. WHEN a conflict error is produced, THE ExceptionHandling_Middleware SHALL set the `title` field to "Conflict"

### Requirement 6: Unprocessable Entity Error Mapping

**User Story:** As a frontend developer, I want domain validation errors (422) to use ProblemDetails format, so that I can display business rule violation messages to users.

#### Acceptance Criteria

1. WHEN a `DomainValidationException` is thrown, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 422
2. WHEN a `DomainValidationException` is thrown, THE ExceptionHandling_Middleware SHALL set the `detail` field to the exception message
3. WHEN a domain validation error is produced, THE ExceptionHandling_Middleware SHALL set the `title` field to "Unprocessable Entity"

### Requirement 7: Rate Limit Error Mapping

**User Story:** As a frontend developer, I want rate limit errors to include retry timing in a structured format, so that I can implement automatic retry logic.

#### Acceptance Criteria

1. WHEN a `RateLimitExceededException` is thrown, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 429
2. WHEN a `RateLimitExceededException` is thrown, THE ExceptionHandling_Middleware SHALL set the `Retry-After` response header to the exception's `RetryAfterSeconds` value
3. WHEN a `RateLimitExceededException` is thrown, THE ExceptionHandling_Middleware SHALL include a `retryAfterSeconds` extension property containing the numeric retry delay
4. WHEN a `RateLimitExceededException` is thrown, THE ExceptionHandling_Middleware SHALL set the `title` field to "Too Many Requests"
5. WHEN a `RateLimitExceededException` is thrown, THE ExceptionHandling_Middleware SHALL set the `detail` field to "Rate limit exceeded. Try again later."

### Requirement 8: Internal Server Error Mapping

**User Story:** As a system operator, I want internal server errors to produce a safe ProblemDetails response without leaking implementation details, so that production errors are traceable via traceId without exposing stack traces.

#### Acceptance Criteria

1. WHEN an unhandled exception occurs that does not match any specific mapping, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 500
2. WHEN an internal server error is produced, THE ExceptionHandling_Middleware SHALL set the `detail` field to "אירעה שגיאה בלתי צפויה. נסה שוב מאוחר יותר."
3. WHEN an internal server error is produced, THE ExceptionHandling_Middleware SHALL set the `title` field to "Internal Server Error"
4. THE ExceptionHandling_Middleware SHALL never include stack traces, exception type names, or inner exception messages in ProblemDetails_Response bodies in production environments

### Requirement 9: Controller-Level Custom Error Migration

**User Story:** As a frontend developer, I want controller-specific error data (like alternative slots) to be available as ProblemDetails extensions, so that I can access both the standard error structure and domain-specific data from a single response.

#### Acceptance Criteria

1. WHEN the ShiftRequestsController rejects a shift request, THE ShiftRequestsController SHALL return an appropriate 4xx ProblemDetails_Response (e.g., 422 for validation, 409 for conflicts, 423 for locked resources) based on the specific rejection reason, with the rejection reason as the `detail` field
2. WHEN the ShiftRequestsController rejects a shift request and alternative slots are available, THE ShiftRequestsController SHALL include an `alternativeSlots` extension property containing the list of available slot objects
3. WHEN the WaitlistController rejects a waitlist operation, THE WaitlistController SHALL return a ProblemDetails_Response with HTTP status 422 and the error message as the `detail` field
4. WHEN a controller returns an error with domain-specific data, THE controller SHALL use ProblemDetails extension properties to carry the additional data instead of custom JSON shapes

### Requirement 10: Bad Request Error Mapping

**User Story:** As a frontend developer, I want bad request errors (invalid operations, argument errors) to use ProblemDetails format, so that I can display meaningful error messages for malformed requests.

#### Acceptance Criteria

1. WHEN an `InvalidOperationException` is thrown, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 400
2. WHEN an `ArgumentException` is thrown, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 400
3. WHEN a bad request error is produced, THE ExceptionHandling_Middleware SHALL set the `title` field to "Bad Request"

### Requirement 11: Payment Required Error Mapping

**User Story:** As a frontend developer, I want payment-required errors to use ProblemDetails format, so that I can trigger upgrade prompts consistently.

#### Acceptance Criteria

1. WHEN a `PaymentRequiredException` is thrown, THE ExceptionHandling_Middleware SHALL return a ProblemDetails_Response with HTTP status 402
2. WHEN a `PaymentRequiredException` is thrown, THE ExceptionHandling_Middleware SHALL set the `detail` field to the exception message
3. WHEN a payment required error is produced, THE ExceptionHandling_Middleware SHALL set the `title` field to "Payment Required"
4. IF ProblemDetails formatting fails for a `PaymentRequiredException`, THEN THE ExceptionHandling_Middleware SHALL still return HTTP status 402 with a minimal JSON body

### Requirement 12: Production Safety

**User Story:** As a security engineer, I want to ensure that no sensitive implementation details leak through error responses in production, so that attackers cannot gain insight into the system internals.

#### Acceptance Criteria

1. WHILE the application is running in a production environment, THE ExceptionHandling_Middleware SHALL exclude stack traces from all ProblemDetails_Response bodies
2. WHILE the application is running in a production environment, THE ExceptionHandling_Middleware SHALL exclude exception type names from all ProblemDetails_Response bodies
3. WHILE the application is running in a production environment, THE ExceptionHandling_Middleware SHALL exclude inner exception details from all ProblemDetails_Response bodies
4. WHILE the application is running in a development environment, THE ExceptionHandling_Middleware SHALL optionally include exception details in ProblemDetails_Response bodies for debugging purposes
5. WHILE the application is running in a production environment, THE ExceptionHandling_Middleware SHALL enforce security restrictions (no stack traces, no exception types, no inner exceptions) regardless of any debug configuration settings

### Requirement 13: Backward Compatibility

**User Story:** As a frontend developer, I want the migration to preserve existing HTTP status codes and error messages, so that the frontend can be updated incrementally without breaking existing error handling.

#### Acceptance Criteria

1. THE ExceptionHandling_Middleware SHALL preserve the same HTTP status codes for each exception type as the current implementation
2. THE ExceptionHandling_Middleware SHALL preserve existing Hebrew error messages in the `detail` field for all mapped exception types
3. WHEN a ProblemDetails_Response is returned, THE ExceptionHandling_Middleware SHALL include the original error message text accessible via the `detail` field, matching the current `error` or `message` field values
