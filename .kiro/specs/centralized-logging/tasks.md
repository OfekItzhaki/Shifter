# Implementation Plan: Centralized Logging

## Overview

Add centralized structured logging to the Shifter platform by introducing a Seq log server to the Docker Compose stack, configuring the Serilog pipeline to ship structured events to Seq with durable buffering, and securing the Seq web UI behind a Caddy reverse proxy with HTTP Basic Authentication. All changes are configuration-driven with minimal code modifications.

## Tasks

- [x] 1. Add Seq service to Docker Compose stack
  - [x] 1.1 Define Seq service in `infra/compose/docker-compose.yml`
    - Add `seq` service using `datalust/seq:latest` image
    - Configure `ACCEPT_EULA=Y` and `SEQ_FIRSTRUN_ADMINPASSWORD` environment variables
    - Expose ports 5341 (ingestion) and 80 (web UI) on internal network only — no host port mapping
    - Add `seq_data:/data` named volume for persistence
    - Define health check: `curl -f http://localhost:5341/health || exit 1`
    - Set `restart: unless-stopped` policy
    - Place on default network so future containers can reach it
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 7.4_

  - [x] 1.2 Add Seq-related environment variables to `.env.example`
    - Add `SEQ_ADMIN_PASSWORD`, `SEQ_UI_USERNAME`, `SEQ_UI_PASSWORD_HASH` with placeholder values
    - _Requirements: 3.5_

- [x] 2. Configure Caddy reverse proxy for Seq web UI
  - [x] 2.1 Create Caddy configuration directory and Caddyfile
    - Create `infra/compose/caddy/Caddyfile` with `logs.shifter.ofeklabs.com` site block
    - Configure `basicauth` using `{$SEQ_UI_USERNAME}` and `{$SEQ_UI_PASSWORD_HASH}` environment variables
    - Configure `reverse_proxy seq:80` to forward authenticated requests to Seq web UI
    - _Requirements: 3.2, 3.3, 3.4, 3.5, 3.6_

  - [x] 2.2 Add Caddy service to `infra/compose/docker-compose.yml`
    - Add `caddy` service using `caddy:2-alpine` image
    - Mount the Caddyfile from `./caddy/Caddyfile`
    - Map port 443 for HTTPS ingress
    - Pass `SEQ_UI_USERNAME` and `SEQ_UI_PASSWORD_HASH` environment variables
    - Set `restart: unless-stopped` policy
    - Ensure Caddy depends on `seq` service
    - _Requirements: 3.1, 3.2, 3.4, 3.5, 3.6, 5.4_

- [x] 3. Checkpoint - Verify infrastructure configuration
  - Ensure Docker Compose file is valid (`docker compose config`), ask the user if questions arise.

- [x] 4. Configure Serilog Seq sink in the API
  - [x] 4.1 Add `Serilog.Sinks.Seq` NuGet package to `Jobuler.Api.csproj`
    - Add package reference for `Serilog.Sinks.Seq`
    - _Requirements: 2.1_

  - [x] 4.2 Update `appsettings.json` with Seq sink and console sink configuration
    - Add `Serilog.Sinks.Seq` to the `Using` array
    - Move console sink from hardcoded `WriteTo.Console()` into `appsettings.json` `WriteTo` array with `JsonFormatter`
    - Add Seq sink to `WriteTo` array with `serverUrl: "http://seq:5341"`, `bufferBaseFilename: "/app/logs/seq-buffer"`, and `retainedInvalidPayloadsLimitBytes: 5242880`
    - Add `Properties` section with `Application: "Shifter.Api"`
    - Ensure `Enrich: ["FromLogContext"]` is present
    - _Requirements: 2.2, 2.3, 2.4, 2.5, 2.6, 7.1, 7.3, 7.5_

  - [x] 4.3 Refactor `Program.cs` Serilog initialization to use configuration-only approach
    - Remove any hardcoded `.WriteTo.Console(...)` or `.WriteTo.Seq(...)` calls
    - Ensure the Serilog block is: `Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();`
    - Verify `Enrich.FromLogContext` is driven by configuration
    - _Requirements: 7.1, 7.3, 7.5_

  - [x] 4.4 Add `Seq__ServerUrl` environment variable override in `docker-compose.yml` for the API service
    - Add `Seq__ServerUrl=http://seq:5341` to the API service environment section
    - This allows local dev to override or disable the Seq sink
    - _Requirements: 2.3, 4.4_

- [x] 5. Checkpoint - Verify Serilog configuration compiles
  - Ensure the project builds successfully (`dotnet build`), ask the user if questions arise.

- [x] 6. Ensure deployment integration
  - [x] 6.1 Verify `deploy-vps.yml` workflow compatibility
    - Confirm the existing `docker compose up -d --build` command will start Seq alongside other services
    - Ensure no `depends_on` from API/web/solver to Seq so a Seq health check failure doesn't block other services
    - Verify no new GitHub secrets are required — all Seq config comes from `.env` on VPS
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x]* 6.2 Write smoke test script to validate configuration files
    - Create a script that parses `docker-compose.yml` and verifies Seq service definition (image, ports, volume, healthcheck, restart policy)
    - Verify `appsettings.json` contains Seq sink with `bufferBaseFilename`
    - Verify `Jobuler.Api.csproj` references `Serilog.Sinks.Seq`
    - Verify Caddyfile uses environment variables for credentials (not hardcoded)
    - _Requirements: 1.1, 1.2, 1.4, 1.6, 1.7, 2.1, 2.5, 3.5_

- [x] 7. Final checkpoint - Ensure all configuration is complete
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- This feature is primarily infrastructure configuration — no new domain models or business logic
- Property-based testing does not apply (no pure functions or business logic with meaningful input variation)
- The API already uses `ReadFrom.Configuration` and `Enrich.FromLogContext`, so the Seq sink is added purely via configuration
- Durable file-based buffering ensures log events survive Seq outages without impacting API availability
- The health check monitor requires no code changes — it communicates over the internal Docker network
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "4.1"] },
    { "id": 1, "tasks": ["2.1", "4.2"] },
    { "id": 2, "tasks": ["2.2", "4.3"] },
    { "id": 3, "tasks": ["4.4", "6.1"] },
    { "id": 4, "tasks": ["6.2"] }
  ]
}
```
