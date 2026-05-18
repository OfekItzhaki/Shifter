# 347 — Recall Notification Service Interface

## Phase

Home Leave Protection — Recall Notification Service

## Purpose

Defines the `IRecallNotificationService` interface in the Application layer so that the recall flow can dispatch push and email notifications to a person being recalled from home leave. This interface establishes the contract that the Infrastructure layer will implement with retry logic and multi-channel delivery.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/HomeLeave/Services/IRecallNotificationService.cs` | New interface defining `SendRecallNotificationAsync` with parameters for spaceId, recalledPersonId, adminName, reason, and expectedReturnAt. Returns `Task<bool>` indicating push delivery success. |

## Key decisions

- **Placed in `Services/` subdirectory** — separates service interfaces from commands/queries/validators, following the pattern of domain-adjacent services.
- **Returns `Task<bool>`** — the boolean indicates whether the push notification was delivered successfully. Email failures are logged but don't affect the return value (fire-and-forget semantics for email).
- **CancellationToken as optional parameter** — follows the project's async pattern for graceful cancellation support.
- **Reason and ExpectedReturnAt are nullable** — matches the optional parameters on `CancelHomeLeaveCommand`.

## How it connects

- The `CancelHomeLeaveCommandHandler` will call this interface after a successful recall (task 9.1).
- The Infrastructure layer will implement this interface in `RecallNotificationService` (task 5.2) using `IPushNotificationSender` and `IEmailSender`.
- Follows Clean Architecture: Application layer defines the contract, Infrastructure provides the implementation.

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Application
```

The interface has no diagnostics and compiles cleanly.

## What comes next

- Task 5.2: Implement `RecallNotificationService` in Infrastructure layer with push retry logic and email delivery.
- Task 9.1: Wire the service into `CancelHomeLeaveCommandHandler` and register in DI.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add IRecallNotificationService interface in Application layer"
```
