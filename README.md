# Shifter — Shift Scheduling SaaS

A secure, multilingual, multi-tenant shift scheduling SaaS for force/platoon/shift-based organizations. Formerly known as Jobuler.

## Overview

Shifter lets organizations manage people, groups, tasks, and constraints, then automatically generate optimized shift schedules using a CP-SAT solver. It supports multiple spaces (tenants), role-based permissions, real-time notifications, and full audit logging.

## Prerequisites

| Tool | Version |
|---|---|
| Node.js | 20+ |
| .NET SDK | 8.0+ |
| Python | 3.11+ |
| PostgreSQL | 16 (local install or Docker) |
| Redis | 7 (optional — in-memory fallback available) |
| Docker Desktop | Optional (for containerised DB/Redis) |

> **No virtualization?** Docker is optional. See [docs/LOCAL-SETUP.md](docs/LOCAL-SETUP.md) for the no-Docker setup path.

## Installation

See **[docs/LOCAL-SETUP.md](docs/LOCAL-SETUP.md)** for full setup instructions.

**Quick start (no Docker):**

```bash
# 1. Install PostgreSQL 16 locally, create database:
#    CREATE USER jobuler WITH PASSWORD 'changeme_local';
#    CREATE DATABASE jobuler OWNER jobuler;

# 2. Run migrations
psql -h localhost -U jobuler -d jobuler -f infra/migrations/000_extensions.sql
# ... run all files in infra/migrations/ in order

# 3. Install deps
cd apps/web && npm install --legacy-peer-deps
cd apps/solver && pip install -r requirements.txt

# 4. Start services (3 terminals)
dotnet run --project apps/api/Jobuler.Api   # http://localhost:5000
python -m uvicorn main:app --port 8000       # http://localhost:8000 (from apps/solver)
npm run dev                                   # http://localhost:3000 (from apps/web)
```

## Configuration

All configuration is via environment variables. Copy `.env.example` and fill in values:

| Variable | Description |
|---|---|
| `DATABASE_URL` | PostgreSQL connection string |
| `REDIS_URL` | Redis connection string |
| `JWT_SECRET` | Secret key for JWT signing (min 32 chars) |
| `JWT_ISSUER` | JWT issuer claim |
| `JWT_AUDIENCE` | JWT audience claim |
| `AI_API_KEY` | OpenAI API key (optional) |
| `SOLVER_BASE_URL` | URL of the Python solver service |

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

1. Build Docker images: `docker compose -f infra/compose/docker-compose.prod.yml build`
2. Push images to your container registry
3. Set all required environment variables in your hosting environment
4. Run DB migrations against the production database
5. Deploy containers — health check endpoint: `GET /health`

The API returns structured JSON logs (Serilog) suitable for ingestion by Seq, ELK, or CloudWatch.

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Next.js 14 + TypeScript |
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

Private — all rights reserved.
