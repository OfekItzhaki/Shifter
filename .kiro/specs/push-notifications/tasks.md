# Implementation Plan: Web Push Notifications

## Overview

Implement Web Push Notifications for Shifter, extending the existing in-app notification system to deliver real-time browser push notifications. The implementation spans the .NET 8 backend (domain entity, MediatR commands, VAPID-based push delivery via WebPush NuGet package), the Next.js frontend (usePushSubscription hook, push settings UI in profile page, i18n), and the existing service worker (push event + notificationclick handlers). The feature is scoped per space (tenant) and degrades gracefully when the browser does not support push.

## Tasks

- [x] 1. Backend domain and data layer
  - [x] 1.1 Create PushSubscription domain entity
    - Create `Jobuler.Domain/Notifications/PushSubscription.cs` implementing `Entity, ITenantScoped`
    - Include properties: SpaceId, UserId, Endpoint, P256dh, Auth
    - Include static `Create` factory method with validation
    - _Requirements: 1.4, 8.3, 9.1, 9.2, 9.3_

  - [x] 1.2 Add EF Core configuration and migration for push_subscriptions table
    - Create `Jobuler.Infrastructure/Persistence/Configurations/PushSubscriptionConfiguration.cs` with Fluent API
    - Define unique constraint on (UserId, SpaceId, Endpoint)
    - Add index on (UserId, SpaceId) for efficient lookup
    - Register DbSet<PushSubscription> in AppDbContext
    - Create EF migration for the new table
    - _Requirements: 8.3, 9.1, 9.2, 9.3_

  - [x] 1.3 Add WebPush NuGet package and VAPID configuration
    - Add `Lib.Net.Http.WebPush` or `WebPush` package to `Jobuler.Infrastructure.csproj`
    - Create `VapidSettings` options class loaded from environment variables (VAPID_PUBLIC_KEY, VAPID_PRIVATE_KEY, VAPID_SUBJECT)
    - Register VAPID configuration in DI (Program.cs)
    - _Requirements: 10.3, 10.4_

- [x] 2. Backend application layer — commands and queries
  - [x] 2.1 Implement CreatePushSubscriptionCommand and handler
    - Create `Jobuler.Application/Notifications/CreatePushSubscriptionCommand.cs` with MediatR IRequest
    - Handler validates input (HTTPS endpoint, non-empty Base64URL p256dh/auth)
    - Handler implements upsert semantics (return success if duplicate exists)
    - Persist PushSubscription entity scoped to userId + spaceId
    - _Requirements: 1.4, 1.5, 9.1, 9.2, 9.3, 9.4_

  - [x] 2.2 Implement DeletePushSubscriptionCommand and handler
    - Create `Jobuler.Application/Notifications/DeletePushSubscriptionCommand.cs`
    - Handler removes matching subscription by (userId, spaceId, endpoint)
    - Return success even if subscription does not exist (idempotent)
    - _Requirements: 2.2, 2.3_

  - [x] 2.3 Implement GetPushSubscriptionStatusQuery and handler
    - Create `Jobuler.Application/Notifications/GetPushSubscriptionStatusQuery.cs`
    - Handler checks if any active subscription exists for (userId, spaceId)
    - Returns `PushSubscriptionStatusResponse { IsSubscribed }`
    - _Requirements: 6.1_

  - [ ]* 2.4 Write property tests for subscription commands (FsCheck)
    - **Property 1: Subscription Persistence** — For any valid subscription data, CreatePushSubscription produces exactly one record with correct values
    - **Property 2: Idempotent Subscribe** — Multiple creates for same (userId, spaceId, endpoint) result in at most one record
    - **Property 3: Unsubscribe Removes Record** — Delete removes the matching record and status returns false
    - **Validates: Requirements 1.4, 1.5, 2.2, 8.3**

- [x] 3. Backend infrastructure — PushNotificationSender
  - [x] 3.1 Define IPushNotificationSender interface in Application layer
    - Create `Jobuler.Application/Notifications/IPushNotificationSender.cs`
    - Define `SendPushToUserAsync(userId, spaceId, payload, ct)` and `SendPushToUsersAsync(userIds, spaceId, payload, ct)`
    - Define `PushPayload` record (Title, Body, Icon, Url, Tag)
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 3.2 Implement PushNotificationSender in Infrastructure layer
    - Create `Jobuler.Infrastructure/Notifications/PushNotificationSender.cs` implementing `IPushNotificationSender`
    - Use IHttpClientFactory for push service requests
    - Encrypt payloads using subscriber's p256dh and auth keys (RFC 8291)
    - Sign requests with VAPID JWT (RFC 8292)
    - Handle 410 Gone → delete subscription from DB
    - Handle 429 Rate Limit → log warning, skip
    - Handle other errors → log error, skip (never throw)
    - _Requirements: 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 10.4_

  - [x] 3.3 Integrate push delivery into NotificationService
    - Modify `Jobuler.Infrastructure/Notifications/NotificationService.cs`
    - Inject `IPushNotificationSender` via constructor
    - After persisting in-app notifications, call `SendPushToUsersAsync` with member IDs
    - Wrap push delivery in try/catch so failures never affect in-app notification persistence
    - Build PushPayload from notification title, body, icon ("/favicon.jpeg"), and URL
    - _Requirements: 3.1, 3.7_

  - [x] 3.4 Register PushNotificationSender in DI
    - Register `IPushNotificationSender` → `PushNotificationSender` in Program.cs
    - Register named HttpClient for push service requests via IHttpClientFactory
    - _Requirements: 3.2, 3.3_

  - [ ]* 3.5 Write property tests for PushNotificationSender (FsCheck)
    - **Property 5: Expired Subscription Cleanup** — When push service returns 410, subscription is deleted from DB
    - **Property 6: Push Failure Isolation** — Push failures never propagate to in-app notification path
    - **Property 8: Tenant Isolation** — Delivery only contacts subscriptions matching the target spaceId
    - **Validates: Requirements 3.5, 3.7, 8.1, 8.2**

- [x] 4. Backend API layer — PushSubscriptionsController
  - [x] 4.1 Create PushSubscriptionsController
    - Create `Jobuler.Api/Controllers/PushSubscriptionsController.cs`
    - Route: `spaces/{spaceId:guid}/push-subscriptions`
    - `[Authorize]` attribute on controller
    - POST Subscribe → dispatches CreatePushSubscriptionCommand, returns 201
    - DELETE Unsubscribe → dispatches DeletePushSubscriptionCommand, returns 204
    - GET status → dispatches GetPushSubscriptionStatusQuery, returns 200
    - Extract CurrentUserId from JWT claims (same pattern as NotificationsController)
    - _Requirements: 1.4, 2.2, 6.1, 10.1, 10.2_

  - [x] 4.2 Add FluentValidation for subscription requests
    - Create validator for CreatePushSubscriptionCommand: endpoint is valid HTTPS URL, p256dh is non-empty Base64URL, auth is non-empty Base64URL
    - Return HTTP 400 with descriptive error on validation failure
    - _Requirements: 9.1, 9.2, 9.3, 9.4_

  - [ ]* 4.3 Write property tests for input validation (FsCheck)
    - **Property 9: Input Validation Rejects Invalid Data** — For any non-HTTPS endpoint or invalid Base64URL fields, controller returns 400 and no data is persisted
    - **Property 10: Ownership Enforcement** — All operations use authenticated user's ID from JWT, never request body
    - **Validates: Requirements 9.1, 9.2, 9.3, 9.4, 10.2**

- [x] 5. Checkpoint — Backend complete
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Frontend — Service Worker push handlers
  - [x] 6.1 Add push event listener to existing service worker
    - Modify `apps/web/public/sw.js`
    - Add `self.addEventListener("push", ...)` handler
    - Parse JSON payload (title, body, icon, url, tag, timestamp)
    - Call `self.registration.showNotification()` with parsed data
    - Handle missing/malformed payload gracefully (no throw)
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 6.2 Add notificationclick event listener to service worker
    - Add `self.addEventListener("notificationclick", ...)` handler
    - Close notification on click
    - Focus existing app window and navigate to payload URL, or open new window
    - _Requirements: 5.1, 5.2, 5.3_

- [x] 7. Frontend — usePushSubscription hook and API
  - [x] 7.1 Create usePushSubscription hook
    - Create `apps/web/lib/hooks/usePushSubscription.ts`
    - Check browser support (Push API + Service Workers)
    - Expose: isSupported, permission, isSubscribed, isLoading, subscribe(), unsubscribe()
    - On mount, check subscription status via GET /spaces/{spaceId}/push-subscriptions/status
    - subscribe(): request permission → pushManager.subscribe(vapidPublicKey) → POST to backend
    - unsubscribe(): DELETE to backend → pushManager unsubscribe
    - Use NEXT_PUBLIC_VAPID_PUBLIC_KEY env var for applicationServerKey
    - Include urlBase64ToUint8Array utility function
    - _Requirements: 1.1, 1.2, 1.3, 1.6, 2.1, 6.1, 6.3, 7.1_

  - [ ]* 7.2 Write unit tests for usePushSubscription hook
    - Test isSupported detection (mock PushManager presence/absence)
    - Test permission flow states (default, granted, denied)
    - Test subscribe/unsubscribe state transitions
    - Test urlBase64ToUint8Array utility
    - _Requirements: 1.1, 1.6, 7.1_

- [x] 8. Frontend — Push Settings UI
  - [x] 8.1 Create PushNotificationSettings component
    - Create `apps/web/components/PushNotificationSettings.tsx`
    - Render toggle switch using usePushSubscription hook
    - Show informational message when permission is denied
    - Hide toggle and show "not supported" message when isSupported is false
    - Show loading/disabled state during subscribe/unsubscribe operations
    - _Requirements: 6.2, 6.3, 7.2, 7.3_

  - [x] 8.2 Integrate PushNotificationSettings into profile page
    - Add PushNotificationSettings component to `apps/web/app/profile/page.tsx`
    - Place it in a card section below the existing NotificationPreferences component
    - Pass current spaceId from context/store
    - _Requirements: 6.2_

  - [x] 8.3 Add i18n keys for push notifications (en, he, ru)
    - Add keys to `apps/web/messages/en.json`: push.enableLabel, push.notSupported, push.permissionDenied, push.enableDescription
    - Add corresponding keys to `apps/web/messages/he.json` and `apps/web/messages/ru.json`
    - _Requirements: 7.2, 7.3_

- [x] 9. Frontend — Environment configuration
  - [x] 9.1 Add VAPID public key environment variable
    - Add `NEXT_PUBLIC_VAPID_PUBLIC_KEY` to `.env.example` with placeholder value
    - Ensure next.config.mjs exposes the variable to the client bundle
    - _Requirements: 1.2, 10.3_

- [x] 10. Checkpoint — Full integration
  - Ensure all tests pass, ask the user if questions arise.

- [x] 11. Final wiring and verification
  - [x] 11.1 End-to-end integration verification
    - Verify NotificationService → PushNotificationSender pipeline is wired correctly
    - Verify PushSubscriptionsController is registered in routing
    - Verify service worker push handler receives and displays notifications
    - Verify subscription lifecycle (create → status check → delete) works end-to-end
    - _Requirements: 1.4, 2.2, 3.1, 4.1, 5.1, 6.1_

  - [ ]* 11.2 Write integration tests for tenant isolation
    - **Property 4: Push Delivery to All Subscriptions** — All active subscriptions for target users in the space receive delivery
    - **Property 8: Tenant Isolation** — Notifications in space A never reach subscriptions in space B
    - **Property 11: Payload Encryption** — Push request body is encrypted, plaintext payload does not appear
    - **Validates: Requirements 3.1, 3.4, 8.1, 8.2, 10.4**

- [x] 12. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- Backend uses C# (.NET 8) with MediatR, EF Core, and FluentValidation
- Frontend uses TypeScript with Next.js, React hooks, and next-intl
- No new npm dependencies needed — Push API and Service Worker API are native browser APIs
- WebPush NuGet package handles VAPID signing and RFC 8291 payload encryption

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.3", "3.1"] },
    { "id": 1, "tasks": ["1.2", "2.1", "2.2", "2.3"] },
    { "id": 2, "tasks": ["2.4", "3.2", "4.1", "4.2"] },
    { "id": 3, "tasks": ["3.3", "3.4", "4.3", "3.5"] },
    { "id": 4, "tasks": ["6.1", "6.2", "7.1", "9.1"] },
    { "id": 5, "tasks": ["7.2", "8.1", "8.3"] },
    { "id": 6, "tasks": ["8.2"] },
    { "id": 7, "tasks": ["11.1", "11.2"] }
  ]
}
```
