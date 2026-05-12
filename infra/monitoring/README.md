# Monitoring & Alerting Setup

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  External Uptime Monitor (Better Stack / UptimeRobot)       │
│  → Pings /health every 60s                                  │
│  → Alerts via email/Slack/Telegram on failure               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  /health endpoint (deep checks)                             │
│  → Verifies PostgreSQL connectivity                         │
│  → Verifies Redis connectivity                              │
│  → Returns 200 (healthy) or 503 (degraded)                  │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│  CloudWatch (AWS-native)                                    │
│  → ECS metrics: CPU, Memory, RunningTaskCount               │
│  → Alarms → SNS → Email                                    │
│  → Logs: JSON-structured via Serilog (auto-captured)        │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────┐
│  Sentry (frontend errors)                                   │
│  → Client-side exceptions                                   │
│  → Set NEXT_PUBLIC_SENTRY_DSN env var to activate           │
└─────────────────────────────────────────────────────────────┘
```

## Setup Steps

### 1. Deploy CloudWatch Alarms

```bash
aws cloudformation deploy \
  --template-file infra/monitoring/cloudwatch-alarms.yml \
  --stack-name jobuler-monitoring \
  --parameter-overrides AlertEmail=your@email.com \
  --region us-east-1
```

After deploying, confirm the SNS subscription via the email you'll receive.

**What this gives you:**
- Alert when API CPU > 80% for 5 min
- Alert when API memory > 85% for 5 min
- Alert when any service has 0 running tasks (service is down)

### 2. External Uptime Monitoring

Choose one (both have free tiers):

#### Option A: Better Stack (recommended)
1. Sign up at https://betterstack.com
2. Create a monitor: `https://shifter.ofeklabs.com/health`
3. Check interval: 60 seconds
4. Alert via: Email + Telegram/Slack
5. Expected status: 200
6. Keyword check: `"status":"healthy"`

#### Option B: UptimeRobot
1. Sign up at https://uptimerobot.com
2. Add HTTP(s) monitor: `https://shifter.ofeklabs.com/health`
3. Interval: 1 minute (free tier: 5 min)
4. Alert contacts: your email

### 3. Activate Sentry

Set the environment variable in your ECS task definition or deploy workflow:
```
NEXT_PUBLIC_SENTRY_DSN=https://your-key@o123.ingest.sentry.io/456
```

### 4. Log Access

Logs are automatically shipped to CloudWatch Logs via the ECS `awslogs` driver.

**View logs:**
```bash
aws logs tail /ecs/jobuler-api --follow --region us-east-1
```

**Search for errors:**
```bash
aws logs filter-log-events \
  --log-group-name /ecs/jobuler-api \
  --filter-pattern '"Level":"Error"' \
  --region us-east-1
```

## Health Endpoints

| Endpoint | Purpose | Auth |
|----------|---------|------|
| `GET /health` | Deep check (DB + Redis) | None |
| `GET /health/live` | Liveness probe (no deps) | None |

### Response format

```json
{
  "status": "healthy",
  "version": "1.0.0",
  "timestamp": "2025-01-15T10:30:00Z",
  "checks": {
    "postgres": "healthy",
    "redis": "healthy"
  }
}
```

Status codes:
- `200` — all systems healthy
- `503` — one or more dependencies unhealthy (status: "degraded")

## Cost

- CloudWatch Alarms: ~$0.30/alarm/month (5 alarms = ~$1.50/month)
- CloudWatch Logs: included with ECS Fargate (first 5GB free)
- Better Stack: free tier (5 monitors, 3-min interval)
- UptimeRobot: free tier (50 monitors, 5-min interval)
- Sentry: free tier (5K errors/month)
