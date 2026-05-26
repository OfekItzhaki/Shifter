# Step 607 — Logging Configuration Smoke Test

## Phase

Phase — Centralized Logging (Verification)

## Purpose

Provides an automated smoke test script that validates all centralized logging configuration files are correctly set up. This ensures the Seq service, Serilog sink, and Caddy reverse proxy configurations remain correct across deployments and refactors.

## What was built

| File | Description |
|------|-------------|
| `scripts/verify-logging-config.ps1` | PowerShell smoke test script that parses and validates docker-compose.yml, appsettings.json, Jobuler.Api.csproj, and Caddyfile for correct logging infrastructure configuration |

## Key decisions

- **PowerShell script** — Cross-platform (works on Windows and Linux with PowerShell Core), no external dependencies needed for YAML/JSON parsing since the checks use regex and `ConvertFrom-Json`.
- **Placed in `scripts/`** — Follows the existing `tools/` pattern but uses a more conventional location for CI/verification scripts.
- **18 individual checks** — Each check maps to specific requirements (1.1, 1.2, 1.4, 1.6, 1.7, 2.1, 2.5, 3.5) with clear pass/fail output.
- **Exit code semantics** — Returns 0 on success, 1 on any failure, making it suitable for CI pipeline integration.

## How it connects

- Validates configuration created in steps 600–606 (Seq service, Caddy, Serilog sink, env overrides)
- Can be integrated into CI pipelines to catch configuration regressions
- Complements the integration tests that require a running Docker stack

## How to run / verify

```powershell
# From repo root
powershell -ExecutionPolicy Bypass -File scripts/verify-logging-config.ps1

# Or with explicit repo root
powershell -ExecutionPolicy Bypass -File scripts/verify-logging-config.ps1 -RepoRoot "C:\path\to\repo"
```

Expected output: 18 PASS checks, 0 failures, exit code 0.

## What comes next

- Integration tests that verify the running Docker stack (Seq ingestion, Caddy auth, durable buffering)
- Potential CI pipeline integration to run this script on every PR

## Git commit

```bash
git add -A && git commit -m "feat(logging): add smoke test script for logging config validation"
```
