# Requirements Document

## Introduction

This document defines the requirements for Web Push Notifications in Shifter. The feature extends the existing in-app notification system to deliver real-time browser push notifications to users who have opted in. It covers the full lifecycle: subscription management (frontend permission flow, backend storage), push delivery (VAPID-authenticated, encrypted payloads sent through standard push services), notification display (service worker), and click-through navigation. The feature is scoped per space (tenant) and degrades gracefully when the browser does not support push or when delivery fails.

## Glossary

- **Push_Subscription**: A record representing a user's opt-in to receive push notifications on a specific device/browser within a specific space. Contains the push service endpoint URL and encryption keys.
- **Push_Service**: An external service (e.g., FCM, Mozilla Push) that accepts encrypted payloads and delivers them to the user's browser via the Web Push protocol.
- **Service_Worker**: A browser-side script that runs in the background, receives push events, and displays native notifications.
- **VAPID**: Voluntary Application Server Identification — a standard (RFC 8292) for authenticating the application server to the push service using a signed JWT.
- **Push_Payload**: A JSON object containing notification title, body, icon, URL, and optional tag, encrypted per RFC 8291 before transmission.
- **PushSubscriptionsController**: The REST API controller managing push subscription CRUD operations.
- **PushNotificationSender**: The infrastructure component responsible for encrypting and dispatching push messages to push services.
- **NotificationService**: The existing application service that creates in-app notifications and now also triggers push delivery.
- **usePushSubscription_Hook**: The React hook managing the push subscription lifecycle on the frontend.
- **Push_Settings_UI**: The toggle component in the user's settings page for enabling/disabling push notifications.

## Requirements

### Requirement 1: Subscribe to Push Notifications

**User Story:** As a space member, I want to subscribe to push notifications on my current device, so that I receive real-time alerts for important events even when the app is not in the foreground.

#### Acceptance Criteria

1. WHEN a user enables push notifications via the Push_Settings_UI, THE usePushSubscription_Hook SHALL request browser notification permission via `Notification.requestPermission()`
2. WHEN the browser grants notification permission, THE usePushSubscription_Hook SHALL create a PushManager subscription using the application's VAPID public key
3. WHEN a PushManager subscription is created, THE usePushSubscription_Hook SHALL send the subscription endpoint, p256dh key, and auth secret to the PushSubscriptionsController via POST
4. WHEN the PushSubscriptionsController receives a valid subscription request, THE PushSubscriptionsController SHALL persist a Push_Subscription entity scoped to the authenticated user and the specified space
5. WHEN a subscription with the same user, space, and endpoint already exists, THE PushSubscriptionsController SHALL return success without creating a duplicate record
6. IF the browser denies notification permission, THEN THE usePushSubscription_Hook SHALL not attempt to create a PushManager subscription and SHALL report the denied state to the Push_Settings_UI

### Requirement 2: Unsubscribe from Push Notifications

**User Story:** As a space member, I want to unsubscribe from push notifications, so that I stop receiving browser alerts when I no longer want them.

#### Acceptance Criteria

1. WHEN a user disables push notifications via the Push_Settings_UI, THE usePushSubscription_Hook SHALL send a delete request with the subscription endpoint to the PushSubscriptionsController
2. WHEN the PushSubscriptionsController receives a valid unsubscribe request, THE PushSubscriptionsController SHALL remove the matching Push_Subscription record for the authenticated user in the specified space
3. WHEN the PushSubscriptionsController receives an unsubscribe request for a non-existent subscription, THE PushSubscriptionsController SHALL return success without error

### Requirement 3: Deliver Push Notifications

**User Story:** As a space member with an active push subscription, I want to receive a browser push notification whenever the system creates an in-app notification for me, so that I am alerted in real time.

#### Acceptance Criteria

1. WHEN the NotificationService creates in-app notifications for one or more users, THE NotificationService SHALL invoke the PushNotificationSender to deliver push messages to all active subscriptions for those users in the relevant space
2. WHEN the PushNotificationSender delivers a push message, THE PushNotificationSender SHALL encrypt the Push_Payload using the subscriber's p256dh public key and auth secret per RFC 8291
3. WHEN the PushNotificationSender delivers a push message, THE PushNotificationSender SHALL sign the request with a VAPID JWT using the server's private key per RFC 8292
4. WHEN a user has multiple active subscriptions in a space, THE PushNotificationSender SHALL deliver the push message to each subscription independently
5. IF the Push_Service returns HTTP 410 (Gone) for a subscription, THEN THE PushNotificationSender SHALL delete that Push_Subscription from the database
6. IF the Push_Service returns HTTP 429 (Rate Limited), THEN THE PushNotificationSender SHALL log a warning and skip that delivery attempt without failing the overall operation
7. IF push delivery fails for any reason, THEN THE NotificationService SHALL still complete the in-app notification persistence successfully

### Requirement 4: Display Push Notifications

**User Story:** As a user receiving a push message, I want to see a native browser notification with the relevant title, body, and icon, so that I can quickly understand what happened.

#### Acceptance Criteria

1. WHEN the Service_Worker receives a push event with a valid payload, THE Service_Worker SHALL display a native notification using the title, body, and icon from the Push_Payload
2. WHEN the Push_Payload includes a tag field, THE Service_Worker SHALL use the tag to replace any existing notification with the same tag
3. IF the Service_Worker receives a push event with missing or malformed payload data, THEN THE Service_Worker SHALL handle the event gracefully without throwing an error

### Requirement 5: Navigate on Notification Click

**User Story:** As a user who sees a push notification, I want to click it and be taken to the relevant page in the app, so that I can act on the notification immediately.

#### Acceptance Criteria

1. WHEN a user clicks a displayed push notification, THE Service_Worker SHALL close the notification and navigate to the URL specified in the Push_Payload
2. WHEN an existing app window is open, THE Service_Worker SHALL focus that window and navigate it to the target URL rather than opening a new window
3. WHEN no app window is open, THE Service_Worker SHALL open a new window at the target URL

### Requirement 6: Subscription Status Check

**User Story:** As a space member, I want the push settings UI to reflect my current subscription state accurately, so that I know whether push is enabled on this device.

#### Acceptance Criteria

1. WHEN the Push_Settings_UI loads, THE usePushSubscription_Hook SHALL check the backend for the current user's subscription status in the active space
2. THE Push_Settings_UI SHALL display the toggle in the correct on/off state based on the subscription status response
3. WHILE the usePushSubscription_Hook is performing a subscribe or unsubscribe operation, THE Push_Settings_UI SHALL show a loading state and disable the toggle

### Requirement 7: Browser Support and Graceful Degradation

**User Story:** As a user on a browser that does not support push notifications, I want the UI to inform me clearly rather than showing broken controls, so that I understand the limitation.

#### Acceptance Criteria

1. WHEN the browser does not support the Push API or Service Workers, THE usePushSubscription_Hook SHALL report `isSupported: false`
2. WHILE `isSupported` is false, THE Push_Settings_UI SHALL hide the push toggle and display an informational message
3. WHEN the user has previously denied notification permission, THE Push_Settings_UI SHALL display a message explaining how to re-enable notifications in browser settings

### Requirement 8: Tenant Isolation

**User Story:** As a space administrator, I want push subscriptions to be isolated per space, so that notifications from one space never reach devices subscribed in a different space.

#### Acceptance Criteria

1. THE PushSubscriptionsController SHALL scope all subscription operations (create, delete, status check) to the space identified in the request URL
2. WHEN the PushNotificationSender queries subscriptions for push delivery, THE PushNotificationSender SHALL filter subscriptions by both user ID and space ID
3. THE Push_Subscription entity SHALL enforce a unique constraint on the combination of user ID, space ID, and endpoint

### Requirement 9: Input Validation

**User Story:** As a system operator, I want subscription requests to be validated before persistence, so that only well-formed subscription data enters the database.

#### Acceptance Criteria

1. WHEN a subscription request is received, THE PushSubscriptionsController SHALL validate that the endpoint is a valid HTTPS URL
2. WHEN a subscription request is received, THE PushSubscriptionsController SHALL validate that the p256dh field is a non-empty Base64URL string
3. WHEN a subscription request is received, THE PushSubscriptionsController SHALL validate that the auth field is a non-empty Base64URL string
4. IF any validation fails, THEN THE PushSubscriptionsController SHALL return HTTP 400 with a descriptive error message without persisting any data

### Requirement 10: Security and Authentication

**User Story:** As a system operator, I want push subscription endpoints to be protected by authentication and VAPID keys to be securely managed, so that the feature does not introduce security vulnerabilities.

#### Acceptance Criteria

1. THE PushSubscriptionsController SHALL require JWT Bearer authentication for all endpoints
2. THE PushSubscriptionsController SHALL only allow a user to manage their own subscriptions — a user cannot create or delete subscriptions for another user
3. THE PushNotificationSender SHALL load VAPID keys from environment variables and never from source code or configuration files committed to version control
4. THE PushNotificationSender SHALL encrypt all push payloads so that only the intended browser can decrypt the content
