# Cloudflare Front Door Plan

Status: Future infrastructure hardening. Do not implement until production DNS, deployment, and rollback flows are stable.

## Goal

Put Cloudflare in front of the public web domain as a lightweight front door for DNS, CDN, WAF, DDoS protection, and basic rate limiting.

## Recommended First Version

Start with Cloudflare Free or Pro for the production web domain.

- Proxy the web domain through Cloudflare.
- Cache only public/static frontend assets.
- Do not cache authenticated API responses.
- Keep API responses private and origin-controlled.
- Add WAF and rate rules for sensitive/high-cost endpoints.
- Upgrade plans only when traffic, support, analytics, or security needs justify it.

## Candidate Rules

Protect these paths first:

- `/api/auth/*`
- `/api/billing/*`
- `/api/import/*`
- `/api/schedule-runs/*`
- `/api/schedule-versions/*`
- `/api/simulation/*`
- `/api/platform/*`
- `/api/admin/*`

Suggested controls:

- Rate limit login, refresh, password reset, and passkey endpoints.
- Rate limit imports, solver triggers, simulation, and schedule publishing.
- Challenge suspicious traffic on admin/platform routes.
- Block obvious abusive countries, ASNs, or user agents only after observing real logs.
- Bypass or disable cache for any request with `Authorization` or auth cookies.

## Origin Requirements

Before enabling proxy mode:

- Confirm Caddy/HTTPS works without Cloudflare.
- Confirm health checks and rollback work.
- Confirm `X-Forwarded-For` / real client IP handling is correct.
- Confirm websocket/SSE behavior if any realtime feature depends on it.
- Confirm cookies remain secure with the final domain and HTTPS mode.

## Rollout Checklist

1. Move DNS to Cloudflare.
2. Add the domain in proxy mode for staging first.
3. Validate login, billing checkout, webhooks, push, imports, and solver flows.
4. Add conservative cache rules for static frontend assets.
5. Add WAF/rate rules in log/simulate mode where available.
6. Turn on blocking/challenge rules gradually.
7. Repeat for production after staging behaves cleanly.

## Open Questions

- Should API live on the same domain under `/api` or a separate `api.*` hostname?
- Should staging be protected with Cloudflare Access instead of Caddy basic auth?
- Which plan is enough once traffic is real: Free, Pro, or Business?
- Do LemonSqueezy webhooks need IP allowlisting or only signature verification?
