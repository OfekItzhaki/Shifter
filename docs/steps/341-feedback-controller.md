# Step 341 — Feedback Controller

## Phase
Feature — Feedback & Bug Report FAB

## Purpose
Expose the `POST /feedback` API endpoint so the frontend can submit bug reports and feedback. The controller extracts the authenticated user's identity from JWT claims, dispatches the `SubmitFeedbackCommand` via MediatR, and maps the `RateLimitExceededException` to a 429 response with a `Retry-After` header.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/FeedbackController.cs` | New controller with `[Authorize]`, route `"feedback"`, and a single `[HttpPost]` action. Includes the `SubmitFeedbackRequest` DTO record. |

## Key decisions

- **DTO in same file**: `SubmitFeedbackRequest` is defined in the same file as the controller, following the pattern used by `SpacesController` (`CreateSpaceRequest`, `TransferOwnershipRequest`).
- **Rate limit handled in controller**: `RateLimitExceededException` is caught explicitly in the action to set the `Retry-After` header and return 429. All other exceptions (validation, 500s) bubble up to `ExceptionHandlingMiddleware`.
- **Claim extraction pattern**: Reuses the `CurrentUserId` property pattern from `SpacesController`, adding `CurrentUserEmail` using `ClaimTypes.Email` (set by `JwtService`).

## How it connects

- Depends on `SubmitFeedbackCommand` (task 1.2) and `RateLimitExceededException` (task 1.2).
- Depends on `FeedbackOptions` configuration (task 1.4) indirectly via the command handler.
- The frontend `useFeedbackSubmission` hook (task 3.3) will call `POST /feedback`.
- Validation is handled by the existing `SubmitFeedbackCommandValidator` + `ExceptionHandlingMiddleware` pipeline.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
```

The project builds successfully with no errors.

## What comes next

- Frontend components (tasks 3.1–3.4) will consume this endpoint.
- Backend property-based tests (task 5.x) and unit tests (task 7.3) will verify controller behavior.

## Git commit

```bash
git add -A && git commit -m "feat(feedback): add FeedbackController with POST /feedback endpoint"
```
