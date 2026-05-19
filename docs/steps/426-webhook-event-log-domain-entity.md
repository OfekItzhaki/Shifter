# 426 — WebhookEventLog Domain Entity

## Phase

LemonSqueezy Billing Integration — Domain Layer

## Purpose

Provides an idempotency mechanism for webhook processing. By storing processed event IDs, the system can detect and skip duplicate webhook deliveries from LemonSqueezy, preventing subscription state corruption.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Billing/WebhookEventLog.cs` | Domain entity with `EventId`, `EventType`, `ProcessedAt` properties and a `Create` static factory method |

## Key decisions

- Extends `Entity` base class (gets `Id` UUID and `CreatedAt` for free) — no need for `ITenantScoped` since webhook events are global, not space-scoped
- Private parameterless constructor for EF Core hydration
- Static factory method `Create(string eventId, string eventType)` sets `ProcessedAt = DateTime.UtcNow` at creation time
- Private setters enforce immutability — once logged, an event record is never modified

## How it connects

- Used by `HandleWebhookCommand` (Application layer) to check idempotency before processing events
- Will be configured in EF Core (Infrastructure layer) with a unique index on `EventId` and an index on `ProcessedAt`
- The database migration (task 2.4) will create the `webhook_event_logs` table

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain/Jobuler.Domain.csproj
```

## What comes next

- Task 2.4: EF Core configuration and database migration for `webhook_event_logs` table
- Task 5.1: `HandleWebhookCommand` uses this entity for duplicate detection

## Git commit

```bash
git add -A && git commit -m "feat(billing): add WebhookEventLog domain entity for idempotent webhook processing"
```
