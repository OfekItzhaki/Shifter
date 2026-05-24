# 478 — Maintenance Page, Legal Pages Overhaul, Cookie Consent

## Phase
Phase 8 — Legal & Compliance

## Purpose
Production-ready legal compliance: a branded maintenance page for Caddy downtime, bilingual (Hebrew + English) Terms of Service and Privacy Policy with updated sections for LemonSqueezy billing and GDPR/Israeli law, a cookie consent banner, and footer links throughout the app.

## What was built

### Files created:

| File | Description |
|------|-------------|
| `infra/compose/maintenance.html` | Static branded 502/maintenance page served by Caddy when web container is down |
| `apps/web/components/CookieConsent.tsx` | Bottom banner cookie consent component using localStorage |

### Files modified:

| File | Change |
|------|--------|
| `infra/deploy-vps.sh` | Added `handle_errors` block to Caddyfile for 502/503/504 → maintenance.html |
| `apps/web/app/terms/page.tsx` | Complete rewrite — bilingual (Hebrew + English), LemonSqueezy billing, liability disclaimer, governing law (Israel), env-based contact email |
| `apps/web/app/privacy/page.tsx` | Complete rewrite — bilingual, PostgreSQL/VPS storage, LemonSqueezy + SendGrid third parties, Israeli Privacy Law, GDPR compliance, env-based contact email |
| `apps/web/app/layout.tsx` | Added CookieConsent component to root layout |
| `apps/web/app/login/page.tsx` | Added footer links to תנאי שימוש and מדיניות פרטיות |
| `apps/web/components/shell/AppShell.tsx` | Added tiny legal links below version number in sidebar |
| `infra/compose/.env.example` | Added `NEXT_PUBLIC_LEGAL_EMAIL` env var |

## Key decisions

1. **Bilingual legal pages** — Hebrew primary (RTL) with English below, separated by a divider. Covers both Israeli and international users.

2. **Contact email from env** — `NEXT_PUBLIC_LEGAL_EMAIL` with fallback to `support@ofeklabs.com`. Easy to change without code changes.

3. **Static maintenance page** — No external dependencies, inline CSS, dark slate background matching the app sidebar (#0f172a). Includes spinner animation and retry button.

4. **Caddy handle_errors** — Only triggers on 502/503/504 (backend down), not on 404 or other errors.

5. **Cookie consent is minimal** — Single bottom bar, stores acceptance in localStorage, links to privacy policy. Non-intrusive.

6. **Legal pages are public** — No AppShell wrapper, no auth required. Server-rendered for SEO.

7. **Liability disclaimer highlighted** — Yellow warning box in both languages emphasizing "the scheduling is a tool, not a guarantee."

8. **Last updated: May 2026** — Static date, not dynamic, so it doesn't change on every render.

## How it connects
- Maintenance page is served by Caddy (configured in `deploy-vps.sh`)
- Legal pages linked from login page footer, AppShell sidebar, and each other
- Cookie consent appears globally via root layout
- `NEXT_PUBLIC_LEGAL_EMAIL` env var used by both legal pages

## How to run / verify
1. Open `infra/compose/maintenance.html` in a browser — see branded maintenance page
2. Navigate to `/terms` — bilingual Terms of Service
3. Navigate to `/privacy` — bilingual Privacy Policy
4. Navigate to `/login` — see footer links to terms and privacy
5. Log in and check sidebar bottom — tiny legal links below version
6. Clear localStorage and reload — cookie consent banner appears at bottom
7. Click "הבנתי" — banner disappears and doesn't return

## What comes next
- Link legal pages from registration page
- Add legal acceptance checkbox to registration flow (optional)

## Git commit

```bash
git add -A && git commit -m "feat(phase8): maintenance page, bilingual legal pages, cookie consent banner"
```
