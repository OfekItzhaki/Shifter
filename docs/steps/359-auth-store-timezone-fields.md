# 359 — Extend authStore with Timezone Fields

## Phase

User Timezone Settings — Frontend auth store integration

## Purpose

Add timezone awareness to the frontend session context. The auth store now holds `timezoneId` (IANA identifier) and `timezoneOffsetMinutes` received from the backend at login and token refresh. This enables all time-rendering components to display times in the user's local timezone without recalculating on every render.

## What was built

| File | Change |
|------|--------|
| `apps/web/lib/api/auth.ts` | Added `timezoneId: string \| null` and `timezoneOffsetMinutes: number \| null` to `LoginResponse` interface |
| `apps/web/lib/webauthn.ts` | Added `timezoneId` and `timezoneOffsetMinutes` to `LoginTokens` interface |
| `apps/web/lib/store/authStore.ts` | Added `timezoneId` and `timezoneOffsetMinutes` to state, login handler, `setTimezone` action, and `partialize` for persistence |
| `apps/web/lib/api/client.ts` | Updated token refresh interceptor to call `setTimezone` with values from refresh response |
| `apps/web/app/login/page.tsx` | Updated biometric login handler to store timezone fields in auth store |

## Key decisions

- **Default to `Asia/Jerusalem` / offset 120** — The app's primary user base is in Israel. If the API returns null/undefined timezone fields (e.g., user hasn't set their country yet), we fall back to this default.
- **Persist timezone in localStorage** — Added to `partialize` so timezone survives page refreshes without requiring a new API call.
- **`setTimezone` action** — Exposed as a public action so the Settings page can update timezone immediately after a location change (no re-login required).
- **Refresh interceptor updates store** — On token refresh, the backend recalculates the offset (handles DST changes between sessions). The interceptor writes the new values to the store.
- **Biometric login also updates store** — The biometric login path bypasses the store's `login()` method, so we explicitly call `useAuthStore.setState()` with timezone fields.

## How it connects

- **Upstream**: Backend tasks 3.1 and 3.2 added `timezoneId` and `timezoneOffsetMinutes` to login and refresh responses.
- **Downstream**: Task 6.2 (`formatLocalTime` utility) will read `timezoneId` from this store to format all displayed times. Task 8.2 (Settings page) will call `setTimezone` after a location update.

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # Should pass with zero errors
```

Functional verification:
1. Log in — check browser DevTools → Application → Local Storage → `jobuler-auth` key contains `timezoneId` and `timezoneOffsetMinutes`
2. Wait for token refresh (or force a 401) — verify timezone fields update in store
3. Log in via biometric — verify timezone fields are set in store

## What comes next

- Task 6.2: Create `formatLocalTime` utility that reads `timezoneId` from the auth store
- Task 8.2: Settings page calls `setTimezone` after country/state update

## Git commit

```bash
git add -A && git commit -m "feat(timezone): extend authStore with timezoneId and timezoneOffsetMinutes"
```
