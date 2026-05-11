# 134 — Offline Schedule Caching (Service Worker)

## Phase
Phase 8 — UX & Mobile

## Purpose
Soldiers often check their schedules in areas with poor or no cellular coverage (underground bunkers, field exercises, etc.). This step adds a service worker that caches schedule data so the app remains usable offline. It also makes the app installable as a PWA on mobile devices.

## What was built

### Files created:

| File | Description |
|------|-------------|
| `apps/web/public/sw.js` | Service worker with network-first caching for schedule APIs, cache-first for static assets, offline fallback page |
| `apps/web/public/manifest.json` | Web App Manifest for PWA installability (Add to Home Screen) |
| `apps/web/lib/hooks/useServiceWorker.ts` | React hook that registers the SW, tracks online/offline state, and handles updates |
| `apps/web/components/shell/OfflineBanner.tsx` | Banner component showing offline status and update availability |

### Files modified:

| File | Change |
|------|--------|
| `apps/web/app/layout.tsx` | Added `<link rel="manifest">` to head |
| `apps/web/app/providers.tsx` | Added `OfflineBanner` component to always render |
| `apps/web/next.config.mjs` | Added `worker-src 'self'` to CSP headers |

## Key decisions

1. **Custom service worker (no library)** — `next-pwa` and `workbox` add complexity and bundle size. A hand-written 100-line SW is simpler, more predictable, and easier to debug.

2. **Network-first for schedule data** — Always try to get fresh data. Only fall back to cache when offline. This prevents stale schedule issues.

3. **Cache-first for static assets** — JS/CSS/images rarely change between deploys. Cache-first gives instant loads.

4. **Never cache auth endpoints** — Tokens and login responses must never be cached for security.

5. **Offline fallback page in Hebrew** — If no cached version exists, show a minimal "no internet" page with a retry button.

6. **PWA manifest** — Enables "Add to Home Screen" on Android/iOS. Start URL points to `/schedule/my-missions` (the most common soldier view).

7. **Update notification** — When a new SW version is available, a blue banner appears with "Update now" button. No forced reloads.

## Caching strategy

| Request type | Strategy | Cache name |
|---|---|---|
| Schedule APIs (`/schedule-versions/current`, `/my-assignments`, `/groups/*/schedule`) | Network-first, cache fallback | `shifter-v1` |
| HTML pages | Network-first, cache fallback | `shifter-v1` |
| Static assets (`.js`, `.css`, `.png`, etc.) | Cache-first, network fallback | `shifter-static-v1` |
| Auth endpoints | Never cached | — |
| Mutations (POST/PUT/DELETE) | Never cached | — |

## How it connects
- Works with the existing localStorage schedule cache in `ScheduleTab.tsx` (step 082) — the SW provides a lower-level network cache, while the app-level cache provides UI-specific offline messaging
- The `OfflineBanner` component uses the same `useServiceWorker` hook that manages registration
- The manifest enables PWA install prompts on mobile browsers

## How to run / verify

1. Build the app: `npm run build`
2. Start production server: `npm start`
3. Open Chrome DevTools → Application → Service Workers → verify "sw.js" is registered
4. Navigate to My Missions or a group schedule
5. Go to DevTools → Network → check "Offline"
6. Reload — the schedule should still display from cache
7. The amber "No connection" banner should appear at the top
8. On mobile: Chrome will show "Add to Home Screen" prompt

## What comes next
- Better notification preferences UI
- Schedule diff view
- Landing page

## Git commit

```bash
git add -A && git commit -m "feat(phase8): offline schedule caching — service worker, PWA manifest, offline banner"
```
