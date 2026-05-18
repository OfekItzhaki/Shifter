# Implementation Plan: Feedback & Bug Report FAB

## Overview

Implement a floating action button (FAB) split into bug report and feedback halves, rendered globally on all authenticated pages. Clicking either half opens a shared modal with a description form. On submission, the backend validates input, enforces per-user rate limiting, and sends an email to the developer via the existing `IEmailSender` service.

The implementation spans frontend (React/TypeScript) and backend (C#/.NET), following the existing layered architecture: Domain → Application → Infrastructure → Api.

## Tasks

- [x] 1. Backend domain and application layer setup
  - [x] 1.1 Create FeedbackSubmission domain entity and EF configuration
    - Create `apps/api/Jobuler.Domain/Feedback/FeedbackSubmission.cs` with `Id`, `UserId`, `SubmittedAtUtc` properties and `Create` factory method
    - Create `apps/api/Jobuler.Infrastructure/Persistence/Configurations/FeedbackSubmissionConfiguration.cs` with table name `feedback_submissions`, composite index on `(UserId, SubmittedAtUtc)`
    - Register `DbSet<FeedbackSubmission>` in `AppDbContext`
    - _Requirements: 7.1_

  - [x] 1.2 Create SubmitFeedbackCommand, validator, and handler
    - Create `apps/api/Jobuler.Application/Feedback/Commands/SubmitFeedbackCommand.cs` as `IRequest` with `UserId`, `UserEmail`, `Type`, `Description`
    - Create `apps/api/Jobuler.Application/Feedback/Validators/SubmitFeedbackCommandValidator.cs` using FluentValidation: type must be "bug" or "feedback", description must be 1–5000 chars after trim
    - Create `apps/api/Jobuler.Application/Feedback/Exceptions/RateLimitExceededException.cs` with `RetryAfterSeconds` property
    - Create `apps/api/Jobuler.Application/Feedback/Commands/SubmitFeedbackCommandHandler.cs`:
      - Query `FeedbackSubmission` count for user in last 60 minutes
      - If count >= 5, throw `RateLimitExceededException` with computed retry-after seconds
      - HTML-escape description (`<`, `>`, `&`, `"`, `'`)
      - Build subject: `"Bug Report: "` or `"Feedback: "` + first 50 chars of description
      - Build HTML body with full escaped description and user email reference
      - Call `IEmailSender.SendAsync`
      - On success, persist `FeedbackSubmission.Create(userId)` to DB
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.6, 6.3, 6.4, 7.1, 7.2_

  - [x] 1.3 Create FeedbackController
    - Create `apps/api/Jobuler.Api/Controllers/FeedbackController.cs` with `[Authorize]`, route `"feedback"`
    - `[HttpPost]` action: extract user ID and email from JWT claims, dispatch `SubmitFeedbackCommand` via MediatR
    - Return 204 on success, catch `RateLimitExceededException` → 429 with `Retry-After` header
    - Validation errors handled by existing `ExceptionHandlingMiddleware`
    - _Requirements: 5.5, 6.1, 6.2, 6.5, 7.2_

  - [x] 1.4 Add Feedback configuration section to appsettings
    - Add `"Feedback": { "DeveloperEmail": "dev@shifter.ofeklabs.com", "MaxSubmissionsPerHour": 5 }` to `appsettings.json`
    - Create a `FeedbackOptions` class and register it in DI via `services.Configure<FeedbackOptions>(configuration.GetSection("Feedback"))`
    - Inject `IOptions<FeedbackOptions>` in the command handler
    - _Requirements: 5.1_

- [x] 2. Checkpoint - Backend verification
  - Ensure all backend code compiles, ask the user if questions arise.

- [x] 3. Frontend FAB and modal components
  - [x] 3.1 Create FeedbackFab component
    - Create `apps/web/components/shell/FeedbackFab.tsx` as a client component
    - Render fixed-position split button: `bottom: 16px; left: 16px; z-index: 1000`
    - Left half: bug icon with `aria-label="Report a bug"`, right half: feedback icon with `aria-label="Submit feedback"`
    - Each half: `min-width: 44px; min-height: 44px`, keyboard-focusable, activatable via Enter/Space
    - Internal state: `modalOpen`, `submissionType: "bug" | "feedback" | null`
    - Clicking a half sets `submissionType` and opens modal; if modal already open, update type
    - Store ref to clicked button for focus restoration
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 8.1, 8.5_

  - [x] 3.2 Create SubmissionModal component
    - Create `apps/web/components/shell/SubmissionModal.tsx` as a client component
    - Props: `open`, `submissionType`, `onClose`, `triggerRef`
    - Title: "Bug Report" or "Feedback" based on `submissionType`
    - Multi-line textarea with `maxLength={5000}`, associated `<label>` via `for`/`id`
    - Character counter: `${description.length}/5000`
    - Submit button disabled when `description.trim().length === 0` or loading
    - Loading state: show spinner, disable submit
    - Success state: show success message, auto-close after 2 seconds
    - Error state: show error message, preserve text
    - Rate limit (429): display retry message with seconds from `Retry-After` header
    - Focus trap: Tab/Shift+Tab cycle within modal
    - Escape key closes modal
    - On close: return focus to `triggerRef`, reset form state
    - `role="dialog"` with `aria-labelledby` pointing to title element
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 4.1, 4.2, 4.3, 4.4, 4.5, 7.3, 8.2, 8.3, 8.4, 8.6, 8.7_

  - [x] 3.3 Create useFeedbackSubmission hook
    - Create `apps/web/hooks/useFeedbackSubmission.ts`
    - Export `submit(payload)`, `status`, `errorMessage`, `retryAfterSeconds`, `reset`
    - Use `apiClient.post("/feedback", payload)` with 10-second timeout
    - Parse `Retry-After` header on 429 responses
    - Handle network errors and timeouts gracefully
    - _Requirements: 4.1, 4.5, 7.3_

  - [x] 3.4 Wire FeedbackFab into global layout
    - Import and render `<FeedbackFab />` in the authenticated app layout (e.g., `apps/web/app/(authenticated)/layout.tsx` or `providers.tsx`)
    - Ensure it renders on all authenticated pages without per-page wiring
    - _Requirements: 1.1_

- [x] 4. Checkpoint - Frontend verification
  - Ensure all frontend code compiles and renders without errors, ask the user if questions arise.

- [ ] 5. Backend property-based tests
  - [ ]* 5.1 Write property test for whitespace-only description rejection
    - **Property 1: Whitespace-only descriptions are rejected**
    - **Validates: Requirements 3.3, 6.3**
    - Generate random whitespace-only strings (spaces, tabs, newlines, empty)
    - Assert validator rejects all of them

  - [ ]* 5.2 Write property test for email subject construction
    - **Property 3: Email subject construction with type prefix and truncation**
    - **Validates: Requirements 5.2, 5.3**
    - Generate random valid descriptions (1–5000 chars) and both submission types
    - Assert subject equals prefix + first 50 chars (or full description if < 50 chars)

  - [ ]* 5.3 Write property test for email body content
    - **Property 4: Email body contains description and sender reference**
    - **Validates: Requirements 5.4**
    - Generate random valid descriptions and random email addresses
    - Assert HTML body contains both the escaped description and the user email

  - [ ]* 5.4 Write property test for HTML escaping
    - **Property 5: HTML escaping in email body**
    - **Validates: Requirements 5.6**
    - Generate strings containing random HTML special characters (`<`, `>`, `&`, `"`, `'`)
    - Assert all special characters are replaced with corresponding HTML entities in the email body

  - [ ]* 5.5 Write property test for description length validation
    - **Property 6: Description length validation**
    - **Validates: Requirements 6.3**
    - Generate strings of length 0–6000 with varying whitespace padding
    - Assert validator accepts trimmed length 1–5000, rejects otherwise

  - [ ]* 5.6 Write property test for submission type validation
    - **Property 7: Submission type enum validation**
    - **Validates: Requirements 6.4**
    - Generate random strings including "bug", "feedback", and arbitrary values
    - Assert validator accepts only "bug" and "feedback"

  - [ ]* 5.7 Write property test for rate limiting with correct Retry-After
    - **Property 8: Rate limiting with correct Retry-After**
    - **Validates: Requirements 7.1, 7.2**
    - Generate random sequences of submission timestamps within/outside 60-min windows
    - Assert rejection when count > 5 in window, and Retry-After equals seconds until oldest expires

- [ ] 6. Frontend property-based tests
  - [ ]* 6.1 Write property test for character count accuracy
    - **Property 2: Character count accuracy**
    - **Validates: Requirements 3.6**
    - Generate random strings of varying length (0–5000+)
    - Assert displayed character count equals exact string length

  - [ ]* 6.2 Write property test for frontend whitespace-only rejection
    - **Property 1: Whitespace-only descriptions are rejected (frontend)**
    - **Validates: Requirements 3.3**
    - Generate random whitespace-only strings
    - Assert submit button is disabled

- [ ] 7. Unit tests
  - [ ]* 7.1 Write unit tests for FeedbackFab component
    - Test FAB renders with correct positioning and z-index
    - Test FAB halves have correct aria-labels
    - Test clicking bug half opens modal with "Bug Report" title
    - Test clicking feedback half opens modal with "Feedback" title
    - Test switching type while modal is open updates title
    - Test FAB halves are keyboard-focusable and activatable via Enter/Space
    - _Requirements: 1.1, 1.4, 2.1, 2.2, 2.3, 2.4, 2.5, 8.1, 8.5_

  - [ ]* 7.2 Write unit tests for SubmissionModal component
    - Test modal shows loading state during submission
    - Test modal shows success and auto-closes after 2s
    - Test modal preserves text on error
    - Test modal shows timeout error after 10s
    - Test modal closes on Escape key
    - Test focus returns to trigger on close
    - Test modal has role="dialog" and accessible name
    - Test character counter displays correct count
    - Test submit button disabled when description is empty/whitespace
    - _Requirements: 3.3, 3.6, 4.2, 4.3, 4.4, 4.5, 7.3, 8.2, 8.3, 8.6, 8.7_

  - [ ]* 7.3 Write unit tests for FeedbackController and handler
    - Test 401 returned without valid JWT
    - Test 400 returned with field-level errors for invalid input
    - Test 429 returned with Retry-After header when rate limited
    - Test 204 returned on successful submission
    - Test email dispatch failure returns 500
    - _Requirements: 5.5, 6.2, 6.5, 7.2_

- [x] 8. Final checkpoint
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- The backend uses C# with FluentValidation, MediatR, and the existing `IEmailSender` infrastructure
- The frontend uses TypeScript/React with the existing `apiClient` (Axios) for API calls
- Rate limiting is per-user via DB timestamp tracking, not IP-based

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.4"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["1.3"] },
    { "id": 3, "tasks": ["3.1", "3.3"] },
    { "id": 4, "tasks": ["3.2"] },
    { "id": 5, "tasks": ["3.4"] },
    { "id": 6, "tasks": ["5.1", "5.2", "5.3", "5.4", "5.5", "5.6", "5.7", "6.1", "6.2"] },
    { "id": 7, "tasks": ["7.1", "7.2", "7.3"] }
  ]
}
```
