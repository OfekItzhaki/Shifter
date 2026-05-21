# Requirements Document

## Introduction

A health check and alerting system for the Shifter API that monitors critical infrastructure services (PostgreSQL, Redis, LemonSqueezy, SendGrid, Solver) and sends push notifications via Pushover when a service transitions from healthy to unhealthy. The system provides a detailed health endpoint for observability and a background monitor that runs independently of the web frontend.

## Glossary

- **Health_Check_Service**: The background hosted service that periodically evaluates the health of all monitored services and triggers alerts on state transitions.
- **Detailed_Health_Endpoint**: The HTTP endpoint at `/health/detailed` that returns per-service health status for all monitored services.
- **Simple_Health_Endpoint**: The existing HTTP endpoint at `/health` used by Docker and deployment orchestrators for basic liveness/readiness checks.
- **Pushover_Notifier**: The component responsible for sending push notifications to the operator's phone via the Pushover API.
- **Service_Status**: The health state of a monitored service, either "healthy" or "unhealthy".
- **State_Transition**: A change in a monitored service's status from healthy to unhealthy or vice versa.
- **Alert_Cooldown**: The minimum time that must elapse between consecutive alerts for the same service.
- **Critical_Service**: A service whose failure degrades core application functionality (PostgreSQL, Redis, Solver).
- **Optional_Service**: A service that is only checked when configured (SendGrid).

## Requirements

### Requirement 1: Detailed Health Check Endpoint

**User Story:** As an operator, I want a detailed health check endpoint that reports the status of each monitored service individually, so that I can quickly identify which specific service is degraded.

#### Acceptance Criteria

1. WHEN a GET request is made to `/health/detailed`, THE Detailed_Health_Endpoint SHALL return a JSON response containing the individual health status of PostgreSQL, Redis, LemonSqueezy API, SendGrid (if configured), and the Solver service.
2. WHEN all monitored services are healthy, THE Detailed_Health_Endpoint SHALL return HTTP status code 200 with an overall status of "healthy".
3. WHEN one or more monitored services are unhealthy, THE Detailed_Health_Endpoint SHALL return HTTP status code 503 with an overall status of "degraded".
4. THE Detailed_Health_Endpoint SHALL include a timestamp and the application version in every response.
5. WHEN the SendGrid API key is not configured, THE Detailed_Health_Endpoint SHALL skip the SendGrid check and report its status as "skipped".
6. THE Detailed_Health_Endpoint SHALL be accessible without authentication.

### Requirement 2: Service Health Checks

**User Story:** As an operator, I want each critical service to be checked with an appropriate connectivity or validity test, so that I have confidence the reported status is accurate.

#### Acceptance Criteria

1. WHEN checking PostgreSQL health, THE Health_Check_Service SHALL execute a lightweight query against the database to verify connectivity.
2. WHEN checking Redis health, THE Health_Check_Service SHALL execute a PING command against the Redis instance.
3. WHEN checking LemonSqueezy API health, THE Health_Check_Service SHALL validate the API key by making an authenticated request to the LemonSqueezy API.
4. WHEN checking SendGrid health, THE Health_Check_Service SHALL validate the API key by making an authenticated request to the SendGrid API.
5. WHEN checking Solver service health, THE Health_Check_Service SHALL make an HTTP request to the Solver service base URL to verify reachability.
6. IF a health check for any service times out after 10 seconds, THEN THE Health_Check_Service SHALL mark that service as unhealthy.

### Requirement 3: Background Health Monitor

**User Story:** As an operator, I want a background process that continuously monitors service health independently of the web frontend, so that I am alerted to outages even when the web layer is unresponsive.

#### Acceptance Criteria

1. THE Health_Check_Service SHALL run as an ASP.NET Core BackgroundService (IHostedService) that executes health checks at a configurable interval.
2. THE Health_Check_Service SHALL use the HEALTH_CHECK_INTERVAL_SECONDS environment variable to determine the polling interval, defaulting to 300 seconds when not configured.
3. WHEN the Health_Check_Service starts, THE Health_Check_Service SHALL perform an initial health check within 30 seconds of application startup.
4. WHEN a monitored service transitions from healthy to unhealthy, THE Health_Check_Service SHALL trigger a Pushover notification.
5. WHEN a monitored service transitions from unhealthy to healthy, THE Health_Check_Service SHALL log a recovery message at Information level.
6. THE Health_Check_Service SHALL maintain in-memory state tracking the last known status of each monitored service.
7. IF the Health_Check_Service encounters an unhandled exception during a check cycle, THEN THE Health_Check_Service SHALL log the error and continue to the next cycle without crashing.

### Requirement 4: Alert Cooldown

**User Story:** As an operator, I want alerts to be rate-limited per service, so that I am not spammed with repeated notifications for the same ongoing outage.

#### Acceptance Criteria

1. THE Health_Check_Service SHALL enforce a cooldown period per service, during which no duplicate alerts are sent for the same service.
2. THE Health_Check_Service SHALL use the HEALTH_CHECK_ALERT_COOLDOWN_SECONDS environment variable to determine the cooldown duration, defaulting to 3600 seconds when not configured.
3. WHEN a service is unhealthy and the cooldown period for that service has not elapsed since the last alert, THE Health_Check_Service SHALL suppress the notification.
4. WHEN a service recovers (transitions to healthy) and then goes unhealthy again, THE Health_Check_Service SHALL reset the cooldown and send a new alert immediately.

### Requirement 5: Pushover Notification Integration

**User Story:** As an operator, I want to receive high-priority push notifications on my phone when a critical service goes down, so that I can respond to outages immediately.

#### Acceptance Criteria

1. WHEN a critical alert is triggered, THE Pushover_Notifier SHALL send an HTTP POST request to `https://api.pushover.net/1/messages.json` with the configured user key and application token.
2. THE Pushover_Notifier SHALL send alerts with priority=1 (high priority) so that the notification bypasses quiet hours and produces an audible alert.
3. THE Pushover_Notifier SHALL include the name of the unhealthy service and the UTC timestamp of when the failure was detected in the notification message.
4. THE Pushover_Notifier SHALL use the PUSHOVER_USER_KEY environment variable for the Pushover user key.
5. THE Pushover_Notifier SHALL use the PUSHOVER_APP_TOKEN environment variable for the Pushover application API token.
6. IF the Pushover API request fails, THEN THE Pushover_Notifier SHALL log the failure at Error level and not retry within the same check cycle.
7. WHILE the PUSHOVER_USER_KEY or PUSHOVER_APP_TOKEN environment variables are not configured, THE Pushover_Notifier SHALL log a warning at startup and skip sending notifications without crashing.

### Requirement 6: Environment Configuration

**User Story:** As an operator, I want all health check and alerting behavior to be configurable via environment variables, so that I can tune intervals and credentials without code changes.

#### Acceptance Criteria

1. THE Health_Check_Service SHALL read the following environment variables: PUSHOVER_USER_KEY, PUSHOVER_APP_TOKEN, HEALTH_CHECK_INTERVAL_SECONDS, HEALTH_CHECK_ALERT_COOLDOWN_SECONDS.
2. WHEN HEALTH_CHECK_INTERVAL_SECONDS is not set, THE Health_Check_Service SHALL default to 300 seconds.
3. WHEN HEALTH_CHECK_ALERT_COOLDOWN_SECONDS is not set, THE Health_Check_Service SHALL default to 3600 seconds.
4. IF HEALTH_CHECK_INTERVAL_SECONDS is set to a value less than 30, THEN THE Health_Check_Service SHALL clamp the interval to 30 seconds and log a warning.

### Requirement 7: Simple Health Endpoint Compatibility

**User Story:** As a DevOps engineer, I want the existing `/health` endpoint to continue working for Docker and deployment health checks, so that existing infrastructure is not disrupted.

#### Acceptance Criteria

1. THE Simple_Health_Endpoint SHALL continue to check PostgreSQL and Redis connectivity and return the existing response format.
2. THE Simple_Health_Endpoint SHALL remain accessible without authentication.
3. THE Simple_Health_Endpoint SHALL not be modified in a way that changes its existing HTTP status code behavior (200 for healthy, 503 for degraded).
