# 350 — Recall Notification Service Implementation

## Phase

Home Leave Protection — Recall Notification Delivery

## Purpose

Implements the `IRecallNotificationService` interface defined in the Application layer. This service handles sending recall notifications to a person being recalled from home leave via push notification (with retry logic) and email (with graceful failure handling).

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Infrastructure/Notifications/RecallNotificationService.cs` | Infrastructure implementation of `IRecallNotificationService` with push retry and email delivery |

## Key decisions

1. **Push retry with exponential backoff**: 3 retries at 1s, 2s, 4s intervals. After all retries fail, logs the error and returns `false` without blocking.
2. **Email failure is non-blocking**: Email send is wrapped in try/catch — failures are logged but never prevent the recall operation from completing.
3. **Person → User resolution**: Resolves the person's `LinkedUserId` from the People table, then fetches the user's email from the Users table. If no linked user exists, logs a warning and returns early.
4. **HTML-encoded output**: Admin name and reason are HTML-encoded in the email body to prevent XSS in email clients.
5. **Notification payload structure**: Push payload uses tag `home_leave_recall` and links to `/schedule`. Body conditionally includes reason and expected return time.

## How it connects

- Implements `IRecallNotificationService` (defined in `Jobuler.Application/HomeLeave/Services/`)
- Uses `IPushNotificationSender` for push delivery (existing infrastructure)
- Uses `IEmailSender` for email delivery (existing infrastructure)
- Will be injected into `CancelHomeLeaveCommandHandler` in task 9.1 (DI registration + dispatch)
- Satisfies Requirements 4.1–4.7 (recall notification delivery, retry, content, graceful failure)

## How to run / verify

```bash
cd apps/api/Jobuler.Infrastructure
dotnet build --no-restore
```

Build should succeed with zero errors.

## What comes next

- Task 5.3: Property test for recall notification content
- Task 5.4: Unit tests for notification delivery behavior
- Task 9.1: Wire `RecallNotificationService` into `CancelHomeLeaveCommand` handler and register in DI

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): implement RecallNotificationService with push retry and email delivery"
```
