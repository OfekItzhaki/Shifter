# 136 — Landing Page / Marketing Page

## Phase
Phase 8 — UX & Growth

## Purpose
New visitors hitting the root URL (`/`) need to understand what Shifter does and be motivated to sign up. Previously, the root page just redirected to `/spaces` (which requires auth). Now unauthenticated users see a polished marketing page.

## What was built

### Files created:

| File | Description |
|------|-------------|
| `apps/web/app/LandingPage.tsx` | Full marketing page with hero, features, how-it-works, stats, CTA, and footer |

### Files modified:

| File | Change |
|------|--------|
| `apps/web/app/page.tsx` | Changed from `redirect("/spaces")` to rendering the LandingPage component |

## Key decisions

1. **Client-side auth check** — Since tokens are in localStorage (not cookies), the landing page renders on the server for SEO, then the client checks for a token and redirects authenticated users to `/spaces`.

2. **Hebrew-first** — The landing page is in Hebrew since the primary audience is Israeli soldiers. The app's language switcher handles other languages once they're logged in.

3. **Dark theme** — Matches the sidebar's dark navy aesthetic. Creates visual contrast with the light app interior, signaling "this is the public face."

4. **No external dependencies** — Pure Tailwind CSS, no animation libraries. Fast load, no layout shift.

5. **Mobile-first design** — All sections stack cleanly on mobile. CTAs are full-width on small screens.

6. **Three CTAs** — Hero section, bottom CTA card, and nav bar all link to `/register`. Multiple conversion points.

## Page sections

1. **Nav** — Logo + login/register buttons
2. **Hero** — Headline, subheadline, two CTA buttons (register + learn more)
3. **Features grid** — 6 cards: auto-scheduling, mobile, fairness, constraints, stats, notifications
4. **How it works** — 3-step flow: Define → Generate → Publish
5. **Stats** — 90% time saved, 0 spreadsheets, 24/7 mobile access
6. **Final CTA** — Blue gradient card with register button
7. **Footer** — Logo, copyright, login/register links

## How it connects
- Links to `/register` and `/login` (existing auth pages)
- Uses the `ShifterLogo` component from step 104
- Authenticated users are redirected to `/spaces` (existing flow)

## How to run / verify
1. Log out (or open incognito)
2. Navigate to `http://localhost:3000/`
3. See the full marketing page
4. Click "הרשמה חינם" → goes to register page
5. Log in → navigate to `/` → should redirect to `/spaces`

## What comes next
- Terms of service / privacy policy pages
- Schedule diff view

## Git commit

```bash
git add -A && git commit -m "feat(phase8): landing page — marketing page with hero, features, CTA for new visitors"
```
