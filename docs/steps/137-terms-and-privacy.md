# 137 — Terms of Service & Privacy Policy Pages

## Phase
Phase 8 — Legal & Compliance

## Purpose
Any production app handling personal data needs legal pages. These are required for app store submissions, GDPR compliance, and user trust. Soldiers need to know how their data is handled.

## What was built

### Files created:

| File | Description |
|------|-------------|
| `apps/web/app/terms/page.tsx` | Terms of Service page — 10 sections covering usage, liability, account management |
| `apps/web/app/privacy/page.tsx` | Privacy Policy page — 10 sections covering data collection, storage, rights, retention |

### Files modified:

| File | Change |
|------|--------|
| `apps/web/app/LandingPage.tsx` | Added "תנאי שימוש" and "פרטיות" links to the footer |

## Key decisions

1. **Hebrew-first** — Written in Hebrew for the Israeli audience. Can be translated later.

2. **Server-rendered** — These are static pages with no client-side interactivity. Good for SEO and fast loading.

3. **Cross-linked** — Terms page links to Privacy, and vice versa. Both link back to the landing page.

4. **Honest and specific** — The privacy policy specifically mentions:
   - BCrypt password hashing
   - TLS encryption
   - Multi-tenant isolation
   - No third-party analytics or tracking
   - localStorage usage (not cookies)
   - 30-day deletion timeline

5. **GDPR-aligned rights** — Users can view, correct, delete, and export their data.

6. **No cookie banner needed** — We don't use cookies for tracking. Only localStorage for auth tokens and preferences.

## Content summary

### Terms of Service
- Service description
- Account responsibilities
- Permitted use
- User content ownership
- Service availability (no SLA guarantee)
- Liability limitation ("AS IS")
- Terms updates
- Account deletion
- Contact info

### Privacy Policy
- Data collected (registration, usage, technical)
- How data is used (scheduling, notifications, security)
- Storage & security measures
- Data sharing rules (within group, to admin, legal)
- Cookies & localStorage
- User rights (view, correct, delete, export)
- Data retention timelines
- Children (16+ only)
- Policy updates
- Contact info

## How it connects
- Linked from the landing page footer (step 136)
- Can be linked from the registration page in the future
- Aligns with the security rules in `.kiro/steering/security-rules.md`

## How to run / verify
1. Navigate to `/terms` — see the Terms of Service page
2. Navigate to `/privacy` — see the Privacy Policy page
3. Click the cross-links between them
4. Check the landing page footer — both links are present

## What comes next
- Schedule diff view
- Payment integration (Paddle)

## Git commit

```bash
git add -A && git commit -m "feat(phase8): terms of service and privacy policy pages"
```
