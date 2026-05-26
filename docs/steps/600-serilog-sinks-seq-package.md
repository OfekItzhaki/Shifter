# 600 — Serilog.Sinks.Seq NuGet Package

## Phase

Centralized Logging — Infrastructure

## Purpose

Adds the `Serilog.Sinks.Seq` NuGet package to the API project so that the Serilog pipeline can ship structured log events to a Seq server. This is a prerequisite for configuring the Seq sink in `appsettings.json`.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Jobuler.Api.csproj` | Added `<PackageReference Include="Serilog.Sinks.Seq" Version="9.0.0" />` |

## Key decisions

- Used version **9.0.0** (latest stable) which supports .NET 8 and durable file-based buffering out of the box.
- Placed the reference alongside the existing Serilog sink packages (`Serilog.AspNetCore`, `Serilog.Sinks.Console`) for logical grouping.

## How it connects

- **Requirement 2.1**: The API container must include `Serilog.Sinks.Seq` as a dependency.
- The next task (4.2) will configure the sink in `appsettings.json` using `WriteTo` configuration.
- The package enables durable buffering via `bufferBaseFilename` (Requirement 2.5).

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet restore
dotnet build
```

The build should succeed with the new package resolved.

## What comes next

- Task 4.2: Configure the Seq sink and console sink in `appsettings.json`
- Task 4.3: Refactor `Program.cs` to use configuration-only Serilog initialization

## Git commit

```bash
git add -A && git commit -m "feat(logging): add Serilog.Sinks.Seq NuGet package to API project"
```
