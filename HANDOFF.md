# Jobuler — Handoff Guide

This document tells you exactly what to do to run, continue, and deploy this project without needing Kiro.

---

## Current state

All 7 phases are complete and pushed to GitHub. The codebase is production-ready in architecture. What's left is environment setup and AWS provisioning.

### What's built
- Full multi-tenant scheduling SaaS backend (ASP.NET Core 8)
- PostgreSQL schema with Row-Level Security
- JWT auth with refresh token rotation
- Spaces, people, groups, tasks, constraints, availability, presence windows
- CP-SAT solver (Python + OR-Tools) with hard constraints and stability/fairness objectives
- Redis job queue + background worker
- Draft → publish → rollback versioning (immutable)
- Audit logs, system logs
- CSV export
- FluentValidation on all commands
- Rate limiting (10 req/min on auth, 200 req/min general)
- AI assistant layer (optional, requires OpenAI API key)
- Next.js frontend: Today/Tomorrow schedule, admin schedule management, people, groups, tasks, constraints, logs
- GitHub Actions CI/CD
- AWS ECS Fargate deployment config

---

## Run locally (5 minutes)

### Prerequisites
- Docker Desktop running
- Node.js 20+
- .NET 8 SDK
- Python 3.11+

### Steps

```bash
# 1. Clone (if on a new machine)
git clone https://github.com/OfekItzhaki/Jobuler.git
cd Jobuler

# 2. Set up environment
cp infra/compose/.env.example infra/compose/.env
# Open infra/compose/.env and change the placeholder passwords

# 3. Start all services
docker compose -f infra/compose/docker-compose.yml up -d

# 4. Wait for postgres to be healthy, then run migrations
./infra/scripts/migrate.sh

# 5. Load demo data
./infra/scripts/seed.sh

# 6. Install frontend dependencies
cd apps/web && npm install

# 7. Run frontend in dev mode
npm run dev
# Open http://localhost:3000
```

### Demo login
- Email: `admin@demo.local`
- Password: `Demo1234!`
- Space: מחלקה א׳ (auto-selected if only one space)

### Run API separately (without Docker)
```bash
cd apps/api
dotnet run --project Jobuler.Api
# API runs on http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

### Run solver separately
```bash
cd apps/solver
pip install -r requirements.txt
uvicorn main:app --reload
# Solver runs on http://localhost:8000
# Health: http://localhost:8000/health
```

### Run tests
```bash
# .NET tests
cd apps/api && dotnet test Jobuler.sln

# Python solver tests
cd apps/solver && pytest tests/ -v
```

---

## Project structure

```
apps/
  api/                    ASP.NET Core 8 API
    Jobuler.Domain/       Domain entities (no dependencies)
    Jobuler.Application/  Commands, queries, validators (MediatR + FluentValidation)
    Jobuler.Infrastructure/ EF Core, Redis, solver client, AI, logging
    Jobuler.Api/          Controllers, middleware, DI wiring
    Jobuler.Tests/        xUnit tests

  solver/                 Python OR-Tools solver service
    models/               Input/output Pydantic models
    solver/               CP-SAT engine, constraints, objectives
    routers/              FastAPI routes
    tests/                pytest tests

  web/                    Next.js 14 frontend
    app/                  Pages (App Router)
    components/           Reusable components
    lib/api/              API client functions
    lib/store/            Zustand state stores
    messages/             i18n translations (he, en, ru)

infra/
  migrations/             PostgreSQL SQL migration files (run in order 000→005)
  scripts/                migrate.sh, seed.sh, test payload
  compose/                docker-compose.yml + .env.example
  docker/                 Dockerfiles for each service
  aws/                    ECS task definitions + setup guide

docs/
  steps/                  Step-by-step build documentation (001→017)
  architecture/           System architecture overview
  kiro-forces-scheduler-tech-spec.md  Original spec
  agents.md               Agent instructions
```

---

## Key API endpoints

All endpoints require `Authorization: Bearer <token>` except auth endpoints.

```
POST /auth/register          Register new user
POST /auth/login             Login → returns accessToken + refreshToken
POST /auth/refresh           Rotate refresh token
POST /auth/logout            Revoke refresh token

GET  /spaces                 List my spaces
POST /spaces                 Create space
GET  /spaces/{id}/people     List people
POST /spaces/{id}/people     Create person
GET  /spaces/{id}/task-types List task types
POST /spaces/{id}/task-types Create task type
GET  /spaces/{id}/task-slots List task slots
POST /spaces/{id}/task-slots Create task slot
GET  /spaces/{id}/constraints List constraints
POST /spaces/{id}/constraints Create constraint
GET  /spaces/{id}/groups     List groups
POST /spaces/{id}/groups     Create group
GET  /spaces/{id}/schedule-versions        List versions
GET  /spaces/{id}/schedule-versions/current Current published schedule
POST /spaces/{id}/schedule-runs/trigger    Trigger solver
POST /spaces/{id}/schedule-versions/{id}/publish  Publish draft
POST /spaces/{id}/schedule-versions/{id}/rollback Rollback
GET  /spaces/{id}/exports/{versionId}/csv  Download CSV
GET  /spaces/{id}/logs       System logs
POST /spaces/{id}/ai/parse-constraint      AI constraint parser
```

Full Swagger docs at `http://localhost:5000/swagger` when running locally.

---

## Deploy to AWS

Follow `infra/aws/README.md` for full instructions. Summary:

1. Create ECR repositories (3: api, solver, web)
2. Create ECS cluster (`jobuler-cluster`)
3. Create RDS PostgreSQL 16 instance
4. Create ElastiCache Redis cluster
5. Store secrets in AWS Secrets Manager:
   - `jobuler/db-connection`
   - `jobuler/redis-connection`
   - `jobuler/jwt-secret`
   - `jobuler/openai-key` (optional)
6. Replace `ACCOUNT_ID` in `infra/aws/ecs-task-api.json` and `ecs-task-solver.json`
7. Register task definitions: `aws ecs register-task-definition --cli-input-json file://infra/aws/ecs-task-api.json`
8. Add GitHub secrets: `AWS_ACCOUNT_ID`, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`
9. Push to `main` → CI/CD builds images, pushes to ECR, deploys to ECS automatically

---

## Enable AI features

Set `AI:ApiKey` in your environment or AWS Secrets Manager:
```bash
# Local: add to infra/compose/.env
AI_API_KEY=sk-your-openai-key

# Or in appsettings.json (never commit real keys)
"AI": { "ApiKey": "sk-...", "Model": "gpt-4o" }
```

Without a key, the AI endpoints return graceful fallback messages and the app works normally.

---

## What's left to build

These require product decisions or external setup:

| Item | Notes |
|---|---|
| PDF export | Add QuestPDF or PuppeteerSharp NuGet package to Jobuler.Infrastructure |
| End-to-end tests | Use Playwright; needs a running test environment |
| Production domain + TLS | Set up in AWS Certificate Manager + ALB listener |

## What was completed in this session (steps 019–022)

| Item | Status |
|---|---|
| Person role assignment UI | ✅ Done — assign/remove roles on person detail page |
| Availability windows UI | ✅ Done — add/view availability and presence windows on person detail page |
| Notification system | ✅ Done — in-app bell, solver triggers notifications, dismiss/dismiss-all |
| Test coverage | ✅ Done — 51 tests passing (role assignment, notifications, availability, integration flow) |
| Build fixes | ✅ Done — MediatR conflict, missing domain entities, AppDbContext moved to Application |

---

## Architecture decisions (do not change without reading the spec)

- Solver is always async — never called synchronously from a controller
- Published schedule versions are immutable — rollback creates a new version
- All tenant data is filtered by `space_id` at both app and DB (RLS) level
- Admin mode is permission-gated server-side — frontend toggle is UX only
- AI never makes scheduling decisions — only parses and explains
- Stability weights: today+tomorrow = 10.0, days 3-4 = 3.0, days 5-7 = 1.0

Full spec: `docs/kiro-forces-scheduler-tech-spec.md`
Build history: `docs/steps/` (001 through 017)
