# 602 — Serilog appsettings.json Seq & Console Sink Configuration

## Phase

Centralized Logging — Serilog Pipeline Configuration

## Purpose

Move the console sink from a hardcoded `WriteTo.Console()` call in `Program.cs` into `appsettings.json` and add the Seq sink configuration with durable file-based buffering. This makes the entire Serilog pipeline configuration-driven via `ReadFrom.Configuration`, enabling future sinks, enrichers, and filters to be added without code changes.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/appsettings.json` | Extended the `Serilog` section with `Using`, `WriteTo` (Console + Seq sinks), `Enrich`, and `Properties` |

### Configuration details

- **`Using`**: Added `Serilog.Sinks.Seq` so the configuration reader can resolve the Seq sink type.
- **Console sink**: Uses `Serilog.Formatting.Json.JsonFormatter, Serilog` for structured JSON output to stdout (keeps `docker logs` functional).
- **Seq sink**: Configured with `serverUrl: "http://seq:5341"`, `bufferBaseFilename: "/app/logs/seq-buffer"` for durable buffering, and `retainedInvalidPayloadsLimitBytes: 5242880` (5 MB cap).
- **`Enrich`**: `["FromLogContext"]` — enables future middleware to push correlation IDs and request metadata onto the log context.
- **`Properties`**: `Application: "Shifter.Api"` — enriches all events with the application name for multi-service filtering in Seq.

## Key decisions

- The `serverUrl` defaults to `http://seq:5341` (Docker internal DNS). This is overridden at runtime via the `Seq__ServerUrl` environment variable in docker-compose.yml (task 4.4).
- Durable buffering (`bufferBaseFilename`) ensures events are not lost during Seq outages — they're written to rolling files and retried with exponential backoff.
- The 5 MB payload limit prevents unbounded disk usage if Seq is down for an extended period.

## How it connects

- **Task 4.1** added the `Serilog.Sinks.Seq` NuGet package — this task configures it.
- **Task 4.3** will remove the hardcoded `.WriteTo.Console()` and `.Enrich.FromLogContext()` from `Program.cs`, leaving only `ReadFrom.Configuration`.
- **Task 4.4** will add the `Seq__ServerUrl` environment variable override in docker-compose.yml.
- **Task 1.1** defined the Seq container that this sink ships logs to.

## How to run / verify

1. Open `apps/api/Jobuler.Api/appsettings.json` and confirm the `Serilog` section contains `Using`, `WriteTo` (Console + Seq), `Enrich`, and `Properties`.
2. Validate JSON: `Get-Content appsettings.json | ConvertFrom-Json` (PowerShell) or any JSON linter.
3. After task 4.3 is complete, run `dotnet build` to confirm the project compiles with the configuration-only approach.

## What comes next

- Task 4.3: Remove hardcoded `WriteTo.Console()` and `Enrich.FromLogContext()` from `Program.cs`.
- Task 4.4: Add `Seq__ServerUrl` environment variable in docker-compose.yml for the API service.

## Git commit

```bash
git add -A && git commit -m "feat(logging): configure Serilog Seq and Console sinks in appsettings.json"
```
