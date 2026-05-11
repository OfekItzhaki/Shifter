# 154 — Push Subscription Domain Entity

## Phase

Push Notifications — Domain Layer

## Purpose

Introduces the `PushSubscription` domain entity that represents a user's opt-in to receive browser push notifications on a specific device within a specific space. This is the foundational data model for the entire Web Push Notifications feature.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Domain/Notifications/PushSubscription.cs` | Domain entity implementing `Entity, ITenantScoped` with properties for SpaceId, UserId, Endpoint, P256dh, Auth, and a static `Create` factory method with input validation |

## Key decisions

- **Extends `Entity` (not `AuditableEntity`)**: Push subscriptions don't need `UpdatedAt` tracking — they are created and deleted, never mutated in place.
- **Validation in factory method**: The `Create` method validates that the endpoint is a valid HTTPS URL, and that p256dh/auth are non-empty. This enforces domain invariants at creation time per Requirements 9.1–9.3.
- **Follows existing `Notification` entity pattern**: Same namespace, same base class, same factory method style for consistency.
- **Private parameterless constructor**: Required for EF Core materialization while preventing invalid construction from outside the class.

## How it connects

- Used by the `CreatePushSubscriptionCommand` handler (Application layer) to persist subscriptions.
- Queried by `PushNotificationSender` (Infrastructure layer) to find active subscriptions for push delivery.
- Will be mapped to a `push_subscriptions` database table via EF Core Fluent API configuration in Infrastructure.
- Implements `ITenantScoped` to participate in RLS and tenant-scoped queries.

## How to run / verify

```bash
cd apps/api/Jobuler.Domain
dotnet build
```

Build should succeed with no errors or warnings related to the new file.

## What comes next

- EF Core configuration for the `PushSubscription` entity (table mapping, unique constraint on userId+spaceId+endpoint, index)
- Database migration to create the `push_subscriptions` table
- `CreatePushSubscriptionCommand` and `DeletePushSubscriptionCommand` in the Application layer

## Git commit

```bash
git add -A && git commit -m "feat(push-notifications): add PushSubscription domain entity"
```
