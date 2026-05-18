# 340 — Feedback Command, Validator, and Handler

## Phase
Feature — Feedback & Bug Report FAB

## Purpose
Implements the core application-layer logic for submitting feedback/bug reports: the MediatR command, FluentValidation validator, rate-limit exception, and the command handler that orchestrates validation, rate limiting, HTML escaping, email dispatch, and persistence.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Feedback/Commands/SubmitFeedbackCommand.cs` | MediatR `IRequest` record with `UserId`, `UserEmail`, `Type`, `Description` |
| `apps/api/Jobuler.Application/Feedback/Validators/SubmitFeedbackCommandValidator.cs` | FluentValidation validator: type must be "bug" or "feedback", description 1–5000 chars after trim |
| `apps/api/Jobuler.Application/Feedback/Exceptions/RateLimitExceededException.cs` | Custom exception with `RetryAfterSeconds` property |
| `apps/api/Jobuler.Application/Feedback/Commands/SubmitFeedbackCommandHandler.cs` | Handler: rate-limit check, HTML escaping, subject/body construction, email dispatch, DB persistence |

## Key decisions

- **Rate limiting via DB query**: Queries `FeedbackSubmissions` for the user in the last 60 minutes. If count >= configured max, throws `RateLimitExceededException` with computed retry-after seconds.
- **Retry-After calculation**: `oldest.SubmittedAtUtc + 60min - now`, clamped to minimum 1 second.
- **HTML escaping order**: `&` is escaped first to avoid double-escaping other entities.
- **Subject truncation**: Uses first 50 characters of the raw (unescaped) description for readability.
- **Email body**: Includes both the escaped description and the user's email as sender reference.
- **Persistence only on success**: `FeedbackSubmission` is persisted only after `IEmailSender.SendAsync` succeeds.

## How it connects

- Depends on `FeedbackSubmission` domain entity (step 338) and `FeedbackOptions` (step 339)
- Uses `IEmailSender` from `Jobuler.Application.Common`
- Uses `AppDbContext.FeedbackSubmissions` DbSet
- Will be dispatched by `FeedbackController` (task 1.3)
- Property-based tests (tasks 5.x) will validate the handler's correctness properties

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
```

All projects (Domain, Application, Infrastructure, Api, Tests) compile successfully.

## What comes next

- Task 1.3: Create `FeedbackController` that dispatches this command and maps exceptions to HTTP status codes.

## Git commit

```bash
git add -A && git commit -m "feat(feedback): add SubmitFeedbackCommand, validator, and handler"
```
