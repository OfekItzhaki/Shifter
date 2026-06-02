# Shifter - Shift Scheduling SaaS

A secure, multilingual, multi-tenant shift scheduling SaaS for force/platoon/shift-based organizations.

## Overview

Shifter is a secure, multilingual, multi-tenant shift scheduling platform for shift-based organizations. It helps teams generate fair, optimized schedules automatically, manage constraints, and notify members in real time.

## Prerequisites

| Tool | Version |
|---|---|
| Node.js | 20+ |
| .NET SDK | 8.0+ |
| Python | 3.11+ |
| PostgreSQL | 16 (local install or Docker) |
| Redis | 7 (optional - in-memory fallback available) |
| Docker Desktop | Optional (for containerised DB/Redis) |

> **No virtualization?** Docker is optional. See [docs/LOCAL-SETUP.md](docs/LOCAL-SETUP.md) for the no-Docker setup path.

## Installation

See **[docs/LOCAL-SETUP.md](docs/LOCAL-SETUP.md)** for full setup instructions.

**Quick start (no Docker):**

```powershell
# 1. Install PostgreSQL 16 locally, create database:
#    CREATE USER jobuler WITH PASSWORD 'changeme_local';
#    CREATE DATABASE jobuler OWNER jobuler;

# 2. Run all SQL migrations in order
$env:PGPASSWORD="changeme_local"
Get-ChildItem infra/migrations/*.sql | Sort-Object Name | ForEach-Object {
    psql -h localhost -U jobuler -d jobuler -f $_.FullName
}

# 3. Install deps
cd apps/web
npm install --legacy-peer-deps
cd ../solver
pip install -r requirements.txt
cd ../..

# 4. Start services in 3 terminals
dotnet run --project apps/api/Jobuler.Api
cd apps/solver; python -m uvicorn main:app --port 8000 --reload
cd apps/web; npm run dev
```

## Configuration

Configuration is read from `apps/api/Jobuler.Api/appsettings.json` and environment variables.
For Docker Compose, copy `infra/compose/.env.example` to `infra/compose/.env` and fill in values.

| Variable | Description |
|---|---|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string for local API runs |
| `Redis__ConnectionString` | Redis connection string; Redis is optional for single-server dev |
| `Jwt__Secret` | Secret key for JWT signing (min 32 chars) |
| `Jwt__Issuer` | JWT issuer claim |
| `Jwt__Audience` | JWT audience claim |
| `AI__ApiKey` | OpenAI API key (optional) |
| `Solver__BaseUrl` | URL of the Python solver service |
| `NEXT_PUBLIC_API_URL` | Frontend base URL for the API |

## Usage

### Running locally (without Docker)

```bash
# Frontend (dev server on port 3000)
cd apps/web && npm install && npm run dev

# API (dev server on port 5000)
cd apps/api && dotnet run --project Jobuler.Api

# Solver (dev server on port 8000)
cd apps/solver && pip install -r requirements.txt && uvicorn main:app --reload
```

### Service URLs

| Service | URL |
|---|---|
| Frontend | http://localhost:3000 |
| API | http://localhost:5000 |
| API Swagger | http://localhost:5000/swagger |
| Solver | http://localhost:8000 |

## Testing

```bash
# Frontend type check
cd apps/web && npx tsc --noEmit

# Frontend lint
cd apps/web && npm run lint

# API build check
cd apps/api && dotnet build

# API tests
cd apps/api && dotnet test
```

## Deployment

1. Build Docker images: `docker compose -f infra/compose/docker-compose.yml build`
2. Push images to your container registry
3. Set all required environment variables in your hosting environment
4. Run DB migrations against the production database
5. Deploy containers - health check endpoint: `GET /health`

The API returns structured JSON logs (Serilog) suitable for ingestion by Seq, ELK, or CloudWatch.

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Next.js 16 + TypeScript |
| Backend API | ASP.NET Core 8 |
| Solver | Python 3.11 + OR-Tools CP-SAT |
| Database | PostgreSQL 16 |
| Cache / Queue | Redis 7 |
| Storage | S3-compatible (MinIO locally) |

## Monorepo Structure

```
shifter/
  apps/
    web/          # Next.js frontend
    api/          # ASP.NET Core API
    solver/       # Python OR-Tools solver service
  infra/
    docker/       # Dockerfiles per service
    compose/      # Docker Compose files
    migrations/   # PostgreSQL migration SQL files
    scripts/      # Seed data and utility scripts
  docs/
    steps/        # Implementation step docs
    architecture/ # Architecture decision records
```

## Contributing

1. Branch from `main` using `feat/` or `fix/` prefix
2. Follow [Conventional Commits](https://www.conventionalcommits.org/) format
3. Run `npx tsc --noEmit` and `dotnet build` before committing
4. Open a PR with a clear description of the change

## License

Private - all rights reserved.
