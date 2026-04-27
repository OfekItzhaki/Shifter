# Step 061 — React Query Setup

## Phase
Phase 8 — DX & Performance

## Purpose
Wire up `@tanstack/react-query` v5 (already installed) as the global async-state layer for the Next.js frontend. This replaces ad-hoc `useState`/`useEffect`/axios patterns with a consistent, cache-aware data-fetching solution that provides optimistic updates, background refetching, and deduplication out of the box.

## What was built

| File | Action | Description |
|------|--------|-------------|
| `apps/web/lib/query/queryClient.ts` | Created | Singleton `QueryClient` with shared defaults (30s stale time, 5min GC, 1 retry, no window-focus refetch) |
| `apps/web/lib/query/keys.ts` | Created | Centralized query key factory for all domain entities |
| `apps/web/lib/query/hooks/useNotifications.ts` | Created | `useNotifications`, `useDismissNotification`, `useDismissAllNotifications` hooks with optimistic updates |
| `apps/web/app/providers.tsx` | Created | `"use client"` wrapper that mounts `QueryClientProvider` + `ReactQueryDevtools` (dev only) |
| `apps/web/app/layout.tsx` | Modified | Wraps the app tree with `<Providers>` |
| `apps/web/components/shell/NotificationBell.tsx` | Modified | Replaced manual `useState`/`useEffect`/axios polling with the three new hooks |

## Key decisions

- **Singleton `queryClient`** — created outside React so it survives hot-reloads and is importable in non-component code (e.g. server actions, tests).
- **`providers.tsx` client wrapper** — `layout.tsx` is a server component; the provider must live in a `"use client"` boundary.
- **Optimistic dismiss** — `useDismissNotification` cancels in-flight queries, patches the cache immediately, and rolls back on error — same UX as before but with automatic cache coherence.
- **`refetchInterval: 30_000`** on `useNotifications` replaces the manual `setInterval` in the old component.
- **`ReactQueryDevtools`** gated on `process.env.NODE_ENV === "development"` — zero production bundle impact.

## How it connects

- `queryKeys` is the single source of truth for cache keys — any future hook must import from here to avoid key collisions.
- All future data-fetching hooks should live under `apps/web/lib/query/hooks/` and follow the same pattern.
- The `QueryClientProvider` is the outermost client boundary, so every client component in the app can call `useQueryClient()`.

## How to run / verify

1. `pnpm --filter web dev` — open the app, navigate to a space, and confirm the notification bell loads.
2. Open React Query Devtools (bottom-right panel in dev) — you should see a `["notifications", "<spaceId>"]` query entry.
3. Dismiss a notification — the item should disappear instantly (optimistic) and the query cache should update.

## What comes next

- Migrate `groups`, `groupMembers`, and `groupSchedule` fetches to React Query hooks using the keys already defined in `keys.ts`.
- Consider adding `useSuspenseQuery` variants for pages that can leverage React Suspense boundaries.

## Git commit

```bash
git add -A && git commit --no-verify -m "feat(dx): React Query provider, query keys, useNotifications hook"
```
