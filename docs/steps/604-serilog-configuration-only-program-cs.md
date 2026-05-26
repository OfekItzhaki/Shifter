# 604 — Serilog Configuration-Only Program.cs

## Phase

Infrastructure — Centralized Logging

## Purpose

Refactor the Serilog initialization in `Program.cs` to use a configuration-only approach. All sink and enricher configuration is now driven entirely by `appsettings.json` via `ReadFrom.Configuration`, satisfying the extensibility requirements (7.1, 7.3, 7.5). This ensures future sinks, enrichers, and filters can be added without code changes.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Program.cs` | Removed hardcoded `.Enrich.FromLogContext()` and `.WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())` calls. The Serilog block is now: `Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger();` |

## Key decisions

- **No bootstrap logger added**: The existing pattern doesn't use a bootstrap logger, and the `ReadFrom.Configuration` approach is sufficient since configuration is available at that point.
- **Console and Seq sinks driven by appsettings.json**: Both sinks were already configured in `appsettings.json` (task 4.2). Removing the hardcoded calls ensures no duplication and full configuration-driven behavior.
- **`Enrich.FromLogContext` via configuration**: The `"Enrich": ["FromLogContext"]` entry in `appsettings.json` replaces the hardcoded `.Enrich.FromLogContext()` call.

## How it connects

- Depends on task 4.2 which added the full Serilog configuration section to `appsettings.json` (Console sink, Seq sink, Enrich, Properties).
- Enables task 4.4 which adds the `Seq__ServerUrl` environment variable override in docker-compose.yml.
- Satisfies Requirements 7.1 (ReadFrom.Configuration), 7.3 (Enrich.FromLogContext via config), and 7.5 (no hardcoded WriteTo calls).

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
# Verify no hardcoded WriteTo or Enrich calls in Program.cs:
grep -n "WriteTo\.\|Enrich\." Program.cs  # should return nothing
```

## What comes next

- Task 4.4: Add `Seq__ServerUrl` environment variable override in docker-compose.yml for the API service.
- Task 5 (checkpoint): Verify the full Serilog configuration compiles and works end-to-end.

## Git commit

```bash
git add -A && git commit -m "feat(logging): refactor Program.cs to configuration-only Serilog initialization"
```
