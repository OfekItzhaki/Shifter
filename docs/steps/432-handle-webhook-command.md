# 432 — HandleWebhookCommand for Dispatch and Idempotency

## Phase

LemonSqueezy Billing Integration — Application Layer (Webhook Event Handlers)

## Purpose

Implements the central webhook event dispatcher that receives parsed webhook events, enforces idempotency via `WebhookEventLog`, isolates test charges, and routes recognized event types to their specific sub-handlers via MediatR.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/Commands/HandleWebhookCommand.cs` | MediatR command + handler. Checks idempotency (event ID in `WebhookEventLogs`), stores event before processing, skips test charges, skips unrecognized event types, and dispatches to sub-handler commands. |
| `apps/api/Jobuler.Application/Billing/Commands/HandleSubscriptionCreatedCommand.cs` | Command record stub for `subscription_created` events (handler implemented in task 5.2). |
| `apps/api/Jobuler.Application/Billing/Commands/HandleSubscriptionUpdatedCommand.cs` | Command record stub for `subscription_updated` events (handler implemented in task 5.3). |
| `apps/api/Jobuler.Application/Billing/Commands/HandleSubscriptionCancelledCommand.cs` | Command record stub for `subscription_cancelled` events (handler implemented in task 5.4). |
| `apps/api/Jobuler.Application/Billing/Commands/HandlePaymentSuccessCommand.cs` | Command record stub for `subscription_payment_success` events (handler implemented in task 5.5). |

## Key decisions

- **Idempotency-first**: The event ID is stored in `WebhookEventLogs` *before* dispatching to sub-handlers. This ensures that even if a sub-handler fails, the event won't be reprocessed on retry (crash-safe idempotency).
- **Test charge isolation**: Metadata `charge_type=test-charge` is checked early and short-circuits all subscription processing, logged at Info level.
- **Unrecognized events are no-ops**: Unknown event types are logged and acknowledged without error, matching LemonSqueezy's expectation of HTTP 200 for all delivered events.
- **Sub-handler stubs**: Command records are created without handlers so the dispatcher compiles. Tasks 5.2–5.5 will add the full handler implementations.
- **Case-insensitive matching**: Event types are compared case-insensitively for robustness.

## How it connects

- Called by the `LemonSqueezyWebhookController` (task 7.1) after signature verification and payload parsing.
- Depends on `WebhookEventLog` domain entity (task 1.2) and `AppDbContext.WebhookEventLogs` DbSet (task 2.4).
- Dispatches to sub-handlers implemented in tasks 5.2–5.5.

## How to run / verify

The project has a pre-existing compilation error in `CreateCheckoutCommand.cs` (references `Jobuler.Infrastructure.Billing` from Application layer — will be resolved when task 4.1 is completed). The `HandleWebhookCommand.cs` itself has zero diagnostics and compiles cleanly in isolation.

## What comes next

- Tasks 5.2–5.5: Implement the sub-handler logic for each event type.
- Task 7.1: Wire the webhook controller to dispatch `HandleWebhookCommand`.

## Git commit

```bash
git add -A && git commit -m "feat(billing): implement HandleWebhookCommand with idempotency and event dispatch"
```
