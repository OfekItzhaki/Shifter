# Requirements Document

## Introduction

Centralized structured logging for the Shifter platform using Serilog and Seq. The API already uses Serilog with console output; this feature adds a Seq container to the Docker Compose stack, configures Serilog to ship structured logs to Seq, secures the Seq web UI behind a reverse proxy so only authenticated administrators can access it, and ensures the existing health check monitor (which runs inside the Docker network) retains internal access to Seq for Pushover alerting. The setup is designed to be extensible for future enhancements such as correlation IDs, enriched structured properties, and multi-service ingestion.

## Glossary

- **Seq**: A structured log server that accepts, indexes, and exposes a web UI for querying structured log events.
- **Serilog**: The .NET structured logging library already used by the Shifter API for console output.
- **Seq_Sink**: The Serilog sink (`Serilog.Sinks.Seq`) that ships log events to a Seq ingestion endpoint over HTTP.
- **Docker_Compose_Stack**: The set of services defined in `infra/compose/docker-compose.yml` that run the Shifter platform on the VPS.
- **Health_Check_Monitor**: The `HealthCheckMonitorService` background service running inside the API container on the Docker network.
- **Reverse_Proxy**: An Nginx or Caddy container (or host-level proxy) that terminates TLS and enforces authentication before forwarding requests to Seq.
- **Seq_Web_UI**: The browser-based interface served by Seq for querying and viewing logs.
- **API_Container**: The ASP.NET Core 8 API service (`api`) in the Docker Compose stack.
- **Ingestion_Endpoint**: The HTTP endpoint exposed by Seq (default port 5341) that accepts structured log events.
- **Admin_User**: The platform operator who accesses the Seq web UI to view and query logs.

## Requirements

### Requirement 1: Seq Container in Docker Compose

**User Story:** As a platform operator, I want a Seq container added to the Docker Compose stack, so that I have a centralized log server running alongside the existing services.

#### Acceptance Criteria

1. THE Docker_Compose_Stack SHALL include a Seq service using the official `datalust/seq` Docker image.
2. WHEN the Docker_Compose_Stack starts, THE Seq service SHALL expose the Ingestion_Endpoint on port 5341 within the Docker network.
3. WHEN the Docker_Compose_Stack starts, THE Seq service SHALL expose the Seq_Web_UI on port 80 within the Docker network.
4. THE Seq service SHALL persist log data using a named Docker volume so that logs survive container restarts.
5. THE Seq service SHALL accept a `ACCEPT_EULA` environment variable set to `Y` to satisfy the Seq license agreement.
6. THE Seq service SHALL define a health check that verifies the Ingestion_Endpoint is responsive.
7. THE Seq service SHALL use the `unless-stopped` restart policy consistent with other services in the Docker_Compose_Stack.

### Requirement 2: Serilog Seq Sink Configuration

**User Story:** As a platform operator, I want the API to ship structured logs to Seq, so that I can query and analyze application events in a centralized location.

#### Acceptance Criteria

1. THE API_Container SHALL include the `Serilog.Sinks.Seq` NuGet package as a dependency.
2. WHEN the API_Container starts, THE Serilog pipeline SHALL send structured log events to the Seq Ingestion_Endpoint.
3. THE Serilog configuration SHALL resolve the Seq ingestion URL from an environment variable (`Seq__ServerUrl`) so that the value is not hardcoded.
4. THE Serilog pipeline SHALL continue writing to the console sink in addition to the Seq_Sink so that `docker logs` remains functional.
5. IF the Seq Ingestion_Endpoint is unreachable, THEN THE Serilog pipeline SHALL buffer events using a durable file-based buffer and retry delivery without crashing the API_Container, so that log events are not lost during extended Seq outages.
6. THE Serilog configuration SHALL enrich all log events with the `Application` property set to `"Shifter.Api"` to support future multi-service ingestion.

### Requirement 3: Seq Web UI Access Control

**User Story:** As a platform operator, I want the Seq web UI secured behind authentication, so that unauthorized users cannot view application logs.

#### Acceptance Criteria

1. THE Docker_Compose_Stack SHALL NOT expose the Seq_Web_UI port directly to the public internet.
2. THE Reverse_Proxy SHALL terminate TLS and forward authenticated requests to the Seq_Web_UI on the internal Docker network.
3. WHEN an unauthenticated request reaches the Reverse_Proxy for the Seq_Web_UI path, THE Reverse_Proxy SHALL respond with HTTP 401 and prompt for credentials; THE Reverse_Proxy SHALL NOT return HTTP 200 for any request lacking valid authentication.
4. THE Reverse_Proxy SHALL use HTTP Basic Authentication or an equivalent mechanism to authenticate the Admin_User.
5. THE Reverse_Proxy credentials SHALL be configured via environment variables and never hardcoded in source control.
6. THE Seq_Web_UI SHALL be accessible at a subdomain or path under the `shifter.ofeklabs.com` domain (e.g., `logs.shifter.ofeklabs.com`).

### Requirement 4: Internal Network Access for Health Check Monitor

**User Story:** As a platform operator, I want the health check monitor to retain access to Seq over the internal Docker network, so that Pushover alerts continue to function without requiring external network access.

#### Acceptance Criteria

1. THE Health_Check_Monitor SHALL access the Seq Ingestion_Endpoint via the internal Docker network hostname (e.g., `http://seq:5341`).
2. THE Health_Check_Monitor SHALL NOT require external network access or authentication to reach the Seq Ingestion_Endpoint.
3. WHEN the Seq service is added to the Docker_Compose_Stack, THE Pushover notification pipeline SHALL continue functioning without configuration changes; other configuration (logging levels, monitoring intervals) MAY be adjusted.
4. THE API_Container SHALL communicate with the Seq service using the Docker internal DNS name, bypassing the Reverse_Proxy.

### Requirement 5: Seq Web UI Log Viewing

**User Story:** As a platform operator, I want to view and query structured logs through the Seq web UI, so that I can diagnose issues and monitor application behavior.

#### Acceptance Criteria

1. WHEN the Admin_User authenticates through the Reverse_Proxy, THE Seq_Web_UI SHALL display all ingested log events with their structured properties.
2. THE Seq_Web_UI SHALL support filtering log events by level, timestamp, and structured property values.
3. THE Seq_Web_UI SHALL display log events within 5 seconds of ingestion under normal operating conditions.
4. WHEN the Admin_User accesses the Seq_Web_UI, THE interface SHALL render correctly AND the browser SHALL maintain an active HTTPS connection through the Reverse_Proxy without mixed-content warnings.

### Requirement 6: Deployment Integration

**User Story:** As a platform operator, I want the Seq container deployed automatically via the existing GitHub Actions workflow, so that centralized logging is available after every deployment without manual intervention.

#### Acceptance Criteria

1. WHEN the deploy-vps workflow runs, THE deployment script SHALL start the Seq service alongside the existing services using `docker compose up`.
2. THE deployment script SHALL ensure the Seq volume is created on first deploy without manual intervention.
3. THE deployment script SHALL use a single `docker compose up` command; IF the Seq container fails its health check, THEN THE deployment script SHALL log a warning while allowing the remaining services (API, web, solver) to continue starting.
4. THE deployment workflow SHALL not require additional secrets beyond the Seq-related environment variables already defined in the `.env` file on the VPS.

### Requirement 7: Extensibility for Future Enhancements

**User Story:** As a developer, I want the logging infrastructure designed for extensibility, so that I can add correlation IDs, additional enrichers, and multi-service ingestion later without rearchitecting.

#### Acceptance Criteria

1. THE Serilog configuration SHALL use `ReadFrom.Configuration` so that additional sinks, enrichers, and filters can be added via `appsettings.json` without code changes.
2. THE Seq service configuration SHALL support API key-based ingestion so that future services can authenticate independently.
3. THE Serilog pipeline SHALL use `Enrich.FromLogContext` so that future middleware can push correlation IDs and request metadata onto the log context.
4. THE Docker_Compose_Stack SHALL define the Seq service on the default network so that future containers can reach the Ingestion_Endpoint without additional network configuration.
5. THE Serilog pipeline SHALL NOT use hardcoded `WriteTo` calls for the Seq sink; all sink configuration SHALL be driven by `appsettings.json` or environment variables via `ReadFrom.Configuration`.
