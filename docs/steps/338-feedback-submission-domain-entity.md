# 338 — FeedbackSubmission Domain Entity & EF Configuration

## Phase

Feature: Feedback & Bug Report FAB

## Purpose

Introduces the `FeedbackSubmission` domain entity used for per-user rate limiting of feedback/bug report submissions. This lightweight entity tracks when a user last submitted, enabling the application layer to enforce a sliding-window rate limit (5 submissions per 60 minutes).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Feedback/FeedbackSubmission.cs` | Domain entity with `Id`, `UserId`, `SubmittedAtUtc` and a `Create` factory method |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/FeedbackSubmissionConfiguration.cs` | EF Core configuration: table `feedback_submissions`, composite index on `(UserId, SubmittedAtUtc)` |
| `apps/api/Jobuler.Application/Persistence/AppDbContext.cs` | Registered `DbSet<FeedbackSubmission>` |

## Key decisions

- **Not tenant-scoped**: Feedback is per-user, not per-space. No `ITenantScoped` implementation needed.
- **No `Entity` base class**: The design spec defines this as a standalone lightweight entity with only the fields needed for rate limiting (`Id`, `UserId`, `SubmittedAtUtc`). It doesn't need `CreatedAt` from the base `Entity` class since `SubmittedAtUtc` serves that purpose.
- **Composite index**: `(UserId, SubmittedAtUtc)` supports efficient queries for counting recent submissions within the sliding window.

## How it connects

- The `SubmitFeedbackCommandHandler` (task 1.2) will query this entity to count recent submissions and persist new records on successful email dispatch.
- The EF configuration is auto-discovered via `ApplyConfigurationsFromAssembly` in `AppDbContext.OnModelCreating`.

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
```

Build should succeed with no new errors.

## What comes next

- Task 1.2: `SubmitFeedbackCommand`, validator, and handler that queries and persists `FeedbackSubmission` records.
- Task 1.4: `FeedbackOptions` configuration class for `DeveloperEmail` and `MaxSubmissionsPerHour`.

## Git commit

```bash
git add -A && git commit -m "feat(feedback): add FeedbackSubmission domain entity and EF configuration"
```
