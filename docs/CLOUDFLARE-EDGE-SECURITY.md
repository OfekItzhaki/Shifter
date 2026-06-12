# Cloudflare Edge Security Baseline

Use this when Shifter is exposed on a public domain. The default recommendation
is Cloudflare Free or Pro in front of the web and API domains, then upgrade only
when traffic, support, compliance, or advanced security requirements justify it.

This guide is intentionally conservative: it protects the high-risk paths first
without relying on Cloudflare as the only security control. Shifter must still
validate auth, tenancy, permissions, billing, imports, admin access, and rate
limits inside the application and API.

## Recommended Starting Plan

| Plan | Use |
|---|---|
| Free | Early production, private pilots, low traffic, basic DNS/TLS/WAF rules |
| Pro | Public launch where managed WAF rules, better bot filtering, and support are useful |
| Business | Customers requiring advanced support, stronger uptime commitments, custom TLS, or stricter security review |
| Enterprise | Regulated/large customers needing contract support, advanced DDoS controls, logs, mTLS, or custom network controls |

Start with Free for internal/pilot domains or Pro for the public SaaS domain.
Do not jump to Enterprise until there is a concrete customer or traffic reason.

## DNS And TLS

Use separate hostnames:

| Hostname | Target |
|---|---|
| `shifter.example.com` | Next.js web service |
| `api-shifter.example.com` | ASP.NET API service |

Recommended Cloudflare settings:

- Proxy both hostnames through Cloudflare.
- Use Full Strict TLS.
- Install a valid origin certificate or public certificate on the origin proxy.
- Redirect HTTP to HTTPS.
- Enable HSTS only after validating every production hostname over HTTPS.
- Keep PostgreSQL, Redis, MinIO, Seq, and internal AI endpoints private. Never
  proxy those services through Cloudflare.

## First WAF Rules

Create explicit rules for sensitive Shifter paths before broad hardening. The
exact syntax depends on the Cloudflare dashboard version, but the rule intent
should stay stable.

| Area | Paths | Baseline action |
|---|---|---|
| Auth | `/api/auth/*` | Managed challenge or rate limit repeated failures |
| Billing | `/api/billing/*`, `/api/webhooks/*` | Rate limit public endpoints; bypass webhook IP/signature checks only if the app validates signatures |
| Imports | `/api/*/imports*`, `/api/*/import*`, `/api/platform/*/import*` | Challenge or stricter rate limit |
| Solver | `/api/*/schedule-runs*`, `/api/*/solver*`, `/api/*/regenerate*` | Authenticated app access only; strict rate limit |
| Admin/platform | `/api/platform/*`, `/api/admin/*` | Challenge, IP allowlist where possible, and app-level re-auth |
| Uploads | `/api/uploads*` | Size limits, extension validation in app, and rate limits |

For customer-hosted installs, let the customer decide whether IP allowlists are
acceptable. For SaaS, use IP allowlists only for platform/admin-only paths that
do not need normal customer access.

## Suggested Rate Limits

Start with soft limits and tune after observing real traffic.

| Path group | Starting point |
|---|---|
| `/api/auth/login` | 5 to 10 attempts per minute per IP/account fingerprint |
| `/api/auth/forgot-password` | 3 to 5 attempts per 10 minutes per IP/email |
| `/api/billing/*` | 30 requests per minute per IP |
| Import endpoints | 5 requests per minute per user/IP |
| Solver/regenerate endpoints | 10 requests per 10 minutes per user/IP |
| Platform/admin endpoints | 30 requests per minute per IP, stricter for mutation-heavy paths |

Keep API-side rate limits too. Cloudflare rules help at the edge, but they do
not replace authenticated, tenant-aware, per-user limits inside Shifter.

## Bot And Abuse Controls

Recommended early settings:

- Enable Cloudflare managed WAF rules in detection/log or low-friction mode
  first, then move noisy rules to block/challenge after review.
- Enable bot fight or bot score controls only if they do not break mobile/PWA
  usage.
- Challenge auth, import, admin, and billing paths more aggressively than read
  paths.
- Keep API JSON responses predictable. Do not let WAF challenge pages reach
  app clients that expect JSON unless the client flow can handle it.

## PWA And Caching

Shifter uses authenticated app data, so Cloudflare must not cache API responses
that include user or workspace data.

Baseline:

- Cache static Next.js assets normally.
- Do not cache `/api/*`.
- Do not cache authenticated HTML responses unless a future rule proves they
  are safe.
- Allow the service worker file and web manifest to be served normally.
- Test PWA install and reconnect behavior after changing Cloudflare caching
  rules.

## Customer-Hosted Notes

For customer-hosted installs:

- Cloudflare is optional. Customers may use their own WAF/reverse proxy.
- If `AI_NO_EXPORT_REQUIRED=true`, do not route private AI endpoints through
  Cloudflare unless the customer explicitly approves that path.
- Use the customer's approved DNS, TLS, logging, and retention rules.
- Disable Cloudflare analytics/log export when the customer does not approve
  Cloudflare as a processor.

## Rollout Checklist

1. Add DNS records for web and API.
2. Enable Full Strict TLS and verify both origins.
3. Disable API caching.
4. Add WAF/rate rules for auth, billing, imports, solver triggers, uploads, and
   admin/platform endpoints.
5. Run login, registration, password reset, PWA install, schedule generation,
   manual self-service, import, billing, and support/contact smoke tests.
6. Review Cloudflare security events for false positives.
7. Tighten rules only after the product flows are confirmed working.

