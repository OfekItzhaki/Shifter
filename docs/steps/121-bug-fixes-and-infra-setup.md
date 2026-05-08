# Step 121 — Bug Fixes, Infra Setup, and Qualification Count Validation

## Phase
Phase 9 — Hardening and Deployment Readiness

## Purpose
Fix the DB connection bug that broke all API requests in Docker, add missing domain entities that prevented the API from building, add qualification count validation to prevent invalid task configurations, and document the local dev setup process.

## What was built

### Bug fixes
- `infra/compose/docker-compose.yml` — Fixed the API connection string to use the container-internal port (5432) instead of the host-mapped port. The `POSTGRES_PORT` and `REDIS_PORT` env vars are only for host-side port mapping; inside the Docker network containers always communicate on their native ports.
- `apps/api/Jobuler.Domain/Logs/AuditLog.cs` — Created missing domain entity with `Create()` factory method matching the `AuditLogger` infrastructure implementation.
- `apps/api/Jobuler.Domain/Logs/SystemLog.cs` — Created missing domain entity with `Create()` factory method matching the `SystemLogger` infrastructure implementation.
- `apps/api/Jobuler.Application/Logs/Queries/GetSystemLogsQuery.cs` — Created missing MediatR query + handler for the logs endpoint.
- `infra/docker/api.Dockerfile` — Added `COPY Jobuler.Tests/*.csproj Jobuler.Tests/` so `dotnet restore` can resolve all projects referenced in the solution file.
- `infra/docker/web.Dockerfile` — Updated from `node:20-alpine` to `node:24-alpine` to match the local npm version (11.x) and avoid `package-lock.json` format mismatch errors.
- `apps/web/.dockerignore` — Added to exclude `node_modules` and `.next` from the Docker build context, reducing context transfer from ~525MB to ~7KB.
- `infra/compose/.env` — Created from `.env.example` with non-conflicting ports (5434, 6381, 5001, 3001, 8001) for machines that already have other services running on the default ports.

### Qualification count validation
- `apps/api/Jobuler.Application/Tasks/Commands/GroupTaskCommands.cs` — Added validation rule to both `CreateGroupTaskCommandValidator` and `UpdateGroupTaskCommandValidator`: the sum of all `QualificationRequirement.Count` values must not exceed `RequiredHeadcount`. This prevents solver payloads where more qualified seats are required than there are people per shift.

## Key decisions
- Internal Docker ports are always the container's native port (5432, 6379), not the host-mapped port. The compose file now hardcodes these in the connection strings rather than using the `POSTGRES_PORT`/`REDIS_PORT` variables, which are only meaningful on the host side.
- `node:24-alpine` is used in the web Dockerfile to match the developer's local npm version. This avoids lock file format mismatches between npm 10 (node:20) and npm 11 (node:24).
- The qualification count rule is a cross-field validator on the command object (not per-requirement), since the constraint is about the total across all requirements vs. the headcount.

## How it connects
- The DB connection fix unblocks all API functionality — login, stats, groups, scheduling.
- The missing `AuditLog`/`SystemLog` domain entities were referenced by `AppDbContext` and `LogsController` but never created, causing compile failures.
- The qualification count validation closes a gap where the solver could receive an impossible constraint (e.g. 3 qualified seats needed but only 2 people per shift).

## How to run / verify
```bash
# Start all services
cd infra/compose
docker compose up

# Verify login works
curl -X POST http://localhost:5001/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@demo.local","password":"Demo1234!"}'

# Verify stats endpoint
curl http://localhost:5001/spaces/e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9/stats/burden \
  -H "Authorization: Bearer <token>"

# Verify qualification count validation (should return 400)
# POST a task with requiredHeadcount=2 and qualificationRequirements summing to 3
```

## What comes next
- AWS infrastructure provisioning (ECR, ECS, RDS) to enable the deploy pipeline
- SendGrid and Twilio credentials to activate real email/WhatsApp notifications
- Group-ownership spec task 19 (property tests checkpoint)

## Git commit
```bash
git add -A && git commit -m "fix(infra): docker db connection, missing domain entities, qualification count validation"
```
