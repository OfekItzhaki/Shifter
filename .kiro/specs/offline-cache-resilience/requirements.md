# Requirements Document

## Introduction

Offline Cache Resilience adds a stale-while-revalidate caching layer to the Shifter web app so users can view their data when offline or when the API server is unavailable. The feature distinguishes between two failure modes (device offline vs. server error), displays appropriate Hebrew-language banners, serves cached data in both cases, silently refreshes when connectivity returns, and disables write operations while disconnected. All cached data is per-user and stored entirely in the browser with zero server-side cost.

## Glossary

- **Cache_Layer**: The client-side caching infrastructure (IndexedDB or Cache API) that stores API responses per user and per space
- **Connectivity_Monitor**: The component that detects whether the device is offline or the API server is unreachable and emits status change events
- **Status_Banner**: A persistent UI notification bar displayed at the top of the app indicating the current connectivity issue
- **Stale_While_Revalidate**: A caching strategy that serves cached data immediately, then fetches fresh data from the network in the background
- **Write_Guard**: The mechanism that prevents or queues mutation operations (POST, PUT, PATCH, DELETE) when the app is disconnected
- **Service_Worker**: The existing registered service worker (sw.js) that intercepts network requests
- **Cached_Endpoint**: A GET API endpoint whose responses are stored in the Cache_Layer for offline access
- **Background_Refresh**: The process of silently fetching fresh data from the API after connectivity is restored

## Requirements

### Requirement 1: Stale-While-Revalidate Caching for Key Endpoints

**User Story:** As a user, I want to see my schedule and group data instantly from cache while fresh data loads in the background, so that the app feels fast and works even when connectivity is poor.

#### Acceptance Criteria

1. WHEN a GET request is made to a Cached_Endpoint, THE Cache_Layer SHALL return the cached response immediately and initiate a background network fetch
2. WHEN the background network fetch succeeds, THE Cache_Layer SHALL update the stored cache entry with the fresh response
3. WHEN the background network fetch succeeds with data different from the cached version, THE Cache_Layer SHALL notify the application so the UI updates with fresh data
4. THE Cache_Layer SHALL cache responses for the following endpoints: GET /spaces/{spaceId}/groups, GET /spaces/{spaceId}/groups/{groupId}/members, GET /spaces/{spaceId}/groups/{groupId}/tasks, GET /spaces/{spaceId}/schedule-versions, GET /spaces/{spaceId}/billing/subscription
5. THE Cache_Layer SHALL store cached data keyed by the authenticated user identifier and the full request URL
6. WHEN no cached response exists for a Cached_Endpoint, THE Cache_Layer SHALL fall through to a normal network request without delay

### Requirement 2: Offline Detection and Banner Display

**User Story:** As a user, I want to see a clear message when I lose internet connectivity, so that I understand why some features may be unavailable.

#### Acceptance Criteria

1. WHEN navigator.onLine transitions to false, THE Connectivity_Monitor SHALL emit an offline event within 1 second
2. WHILE the device is offline, THE Status_Banner SHALL display the text "אתה לא מחובר לאינטרנט" with a warning visual style
3. WHEN navigator.onLine transitions back to true, THE Connectivity_Monitor SHALL emit an online event within 1 second
4. WHEN an online event is emitted, THE Status_Banner SHALL be dismissed within 2 seconds

### Requirement 3: Server Unavailability Detection and Banner Display

**User Story:** As a user, I want to see a specific message when the server is down but my internet works, so that I know the problem is temporary and not on my end.

#### Acceptance Criteria

1. WHEN the device is online and an API request returns a 5xx status code or a network error, THE Connectivity_Monitor SHALL emit a server-unavailable event
2. WHILE the server is unavailable, THE Status_Banner SHALL display the text "השרת אינו זמין כרגע, נסה שוב מאוחר יותר" with an error visual style
3. WHEN a subsequent API request succeeds after a server-unavailable state, THE Connectivity_Monitor SHALL emit a server-recovered event
4. WHEN a server-recovered event is emitted, THE Status_Banner SHALL be dismissed within 2 seconds
5. THE Connectivity_Monitor SHALL distinguish between device-offline and server-unavailable states and display only the appropriate banner at any given time

### Requirement 4: Cached Data Served During Failures

**User Story:** As a user, I want to still view my last-known schedule and groups when offline or when the server is down, so that I can reference my data without connectivity.

#### Acceptance Criteria

1. WHILE the device is offline, THE Cache_Layer SHALL serve the most recent cached response for any Cached_Endpoint request
2. WHILE the server is unavailable, THE Cache_Layer SHALL serve the most recent cached response for any Cached_Endpoint request
3. WHEN a Cached_Endpoint request fails and no cached response exists, THE Cache_Layer SHALL return an empty-state indicator so the UI can display a "no data available" message
4. THE Cache_Layer SHALL preserve cached data across browser sessions until explicitly invalidated or the user logs out

### Requirement 5: Background Data Refresh on Reconnection

**User Story:** As a user, I want my data to refresh automatically when connectivity returns, so that I always see up-to-date information without manually reloading.

#### Acceptance Criteria

1. WHEN the Connectivity_Monitor emits an online event or a server-recovered event, THE Background_Refresh SHALL re-fetch all Cached_Endpoints for the current space within 5 seconds
2. WHEN a Background_Refresh fetch succeeds, THE Cache_Layer SHALL update the cached entry and notify the UI to re-render with fresh data
3. THE Background_Refresh SHALL execute fetches without displaying loading spinners or blocking user interaction
4. IF a Background_Refresh fetch fails, THEN THE Cache_Layer SHALL retain the existing cached data and schedule a retry after 30 seconds

### Requirement 6: Write Operation Guard

**User Story:** As a user, I want to be prevented from performing actions that require server connectivity when offline, so that I do not lose data or trigger errors.

#### Acceptance Criteria

1. WHILE the device is offline or the server is unavailable, THE Write_Guard SHALL disable UI controls that trigger mutation operations (create, update, delete)
2. WHILE the device is offline or the server is unavailable, THE Write_Guard SHALL prevent submission of mutation API requests
3. WHEN a user attempts a disabled write action, THE Write_Guard SHALL display a tooltip or message explaining that the action requires connectivity
4. WHEN connectivity is restored, THE Write_Guard SHALL re-enable all previously disabled mutation controls within 2 seconds

### Requirement 7: Per-User Cache Isolation

**User Story:** As a user, I want my cached data to be private to my account, so that another user on the same device cannot see my schedule or group information.

#### Acceptance Criteria

1. THE Cache_Layer SHALL partition all cached data by the authenticated user identifier
2. WHEN a user logs out, THE Cache_Layer SHALL clear all cached data associated with that user
3. WHEN a different user logs in on the same device, THE Cache_Layer SHALL serve only cached data belonging to that user
4. THE Cache_Layer SHALL store all data in browser-local storage (IndexedDB or Cache API) with zero server-side storage cost

### Requirement 8: Performance — Cache Must Not Degrade App Speed

**User Story:** As a user, I want the caching layer to make the app feel faster, not slower, so that my experience improves with the feature enabled.

#### Acceptance Criteria

1. THE Cache_Layer SHALL return cached responses in under 50 milliseconds for any Cached_Endpoint
2. THE Cache_Layer SHALL not block the main thread during cache read or write operations
3. WHEN both a cached response and a network response are available, THE Cache_Layer SHALL render the cached response first and update the UI only when the network response differs
4. THE Cache_Layer SHALL limit total storage per user to 50 MB and evict least-recently-used entries when the limit is reached
