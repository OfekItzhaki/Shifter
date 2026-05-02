# Local Development Setup

Two options depending on your machine capabilities.

---

## Option A — Without Docker (laptop, virtualization OFF)

This is the primary path for machines where Docker/virtualization is unavailable.

### Prerequisites

| Tool | Version | Download |
|---|---|---|
| Node.js | 20+ | https://nodejs.org |
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| Python | 3.11+ | https://python.org |
| PostgreSQL | 16 | https://www.postgresql.org/download/windows/ |

> **Redis is optional.** The API automatically falls back to an in-memory queue when Redis is unavailable. This works fine for single-machine development.

### 1. Install PostgreSQL

Download and install PostgreSQL 16 from https://www.postgresql.org/download/windows/

During installation:
- Set the superuser password (remember it)
- Keep the default port: **5432**

After installation, open **pgAdmin** or **psql** and create the database:

```sql
CREATE USER jobuler WITH PASSWORD 'changeme_local';
CREATE DATABASE jobuler OWNER jobuler;
GRANT ALL PRIVILEGES ON DATABASE jobuler TO jobuler;
```

### 2. Run database migrations

```bash
# From the repo root — runs all SQL migration files in order
$env:PGPASSWORD="changeme_local"
Get-ChildItem infra/migrations/*.sql | Sort-Object Name | ForEach-Object {
    psql -h localhost -U jobuler -d jobuler -f $_.FullName
}
```

Or run them one by one in pgAdmin by opening each file in `infra/migrations/` in order (000 → 028).

### 3. Load seed data (optional but recommended)

```bash
$env:PGPASSWORD="changeme_local"
psql -h localhost -U jobuler -d jobuler -f infra/scripts/seed.sql
```

Demo login after seeding:
- Email: `admin@demo.local`
- Password: `Demo1234!`

### 4. Install dependencies

```bash
# Frontend
cd apps/web && npm install --legacy-peer-deps

# .NET (auto-restores on build)
dotnet restore apps/api/Jobuler.sln

# Python solver
cd apps/solver && pip install -r requirements.txt
```

### 5. Start services (3 terminals)

**Terminal 1 — API:**
```bash
cd apps/api
dotnet run --project Jobuler.Api
# Runs on http://localhost:5000
# Swagger: http://localhost:5000/swagger
```

**Terminal 2 — Solver:**
```bash
cd apps/solver
python -m uvicorn main:app --port 8000 --reload
# Runs on http://localhost:8000
```

**Terminal 3 — Frontend:**
```bash
cd apps/web
npm run dev
# Runs on http://localhost:3000
```

### Notes for no-Docker setup

- Redis is **not required** — the API uses an in-memory job queue automatically
- The solver queue works fine in single-process mode without Redis
- Notifications (WhatsApp/email) work without Redis — they're sent directly from the publish command
- File uploads use local disk storage (`apps/api/wwwroot/uploads/`) instead of S3

---

## Option B — With Docker (requires virtualization)

If Docker Desktop is available and virtualization is enabled:

```bash
# 1. Copy env template
cp infra/compose/.env.example infra/compose/.env

# 2. Start PostgreSQL + Redis
docker compose -f infra/compose/docker-compose.yml up -d postgres redis

# 3. Run migrations
$env:PGPASSWORD="changeme_local"
Get-ChildItem infra/migrations/*.sql | Sort-Object Name | ForEach-Object {
    psql -h localhost -p 5432 -U jobuler -d jobuler -f $_.FullName
}

# 4. Load seed data
psql -h localhost -p 5432 -U jobuler -d jobuler -f infra/scripts/seed.sql

# 5. Start app services (same as Option A, steps 4-5)
```

---

## Verify everything is working

```bash
# API health
curl http://localhost:5000/health

# Solver health  
curl http://localhost:8000/health

# Frontend
# Open http://localhost:3000 in browser
```

---

## Configuration

The API reads config from `apps/api/Jobuler.Api/appsettings.json`.

For local dev, the defaults work out of the box if PostgreSQL is on `localhost:5432` with username `jobuler` and password `changeme_local`.

To override without editing the file, set environment variables:
```bash
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=jobuler;Username=jobuler;Password=yourpassword"
$env:Jwt__Secret = "your-secret-min-32-chars"
```

---

## Messaging (optional)

WhatsApp and email notifications are disabled by default (no-op mode).
To enable, add to `appsettings.json` or environment variables:

```json
"SendGrid": { "ApiKey": "SG.your-key" },
"Twilio": { "AccountSid": "AC...", "AuthToken": "...", "WhatsAppFrom": "whatsapp:+14155238886" }
```
