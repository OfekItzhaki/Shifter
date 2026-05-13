# Step 243 — useHomeLeavePreview Custom Hook

## Phase
Feature — Home-Leave Slider (Frontend Preview Integration)

## Purpose
Provide a React hook that fetches home-leave preview data from the API with debouncing, request cancellation, and stale response handling. This enables the slider to trigger preview requests as the admin moves it, without overwhelming the server or showing outdated results.

## What was built

| File | Description |
|------|-------------|
| `apps/web/hooks/useHomeLeavePreview.ts` | Custom hook accepting `spaceId`, `groupId`, `balanceValue`; debounces 500ms, cancels in-flight requests via AbortController, ignores stale responses, returns `{ data, isLoading, error }` |
| `apps/web/lib/api/homeLeave.ts` | Updated `getHomeLeavePreview` to accept an optional `AbortSignal` parameter for request cancellation |

## Key decisions

1. **useRef for AbortController and request counter** — Refs avoid re-renders and persist across effect cycles. The request counter ensures stale responses are discarded even if the abort doesn't fire in time.
2. **setTimeout for debouncing** — A 500ms `setTimeout` inside `useEffect` with cleanup provides simple, dependency-free debouncing without external libraries.
3. **AbortController over axios CancelToken** — CancelToken is deprecated in axios; AbortController is the modern standard and natively supported.
4. **Error message in Hebrew** — Matches requirement 6.5: "לא ניתן לטעון תצוגה מקדימה" displayed on failure.
5. **Guard on truthy parameters** — The hook only fires when `spaceId`, `groupId`, and `balanceValue` are all truthy, preventing unnecessary requests during initial render.

## How it connects

- Consumed by the `ImpactSummary` / slider integration (task 8.4) to wire slider changes to live preview updates.
- Calls `getHomeLeavePreview` from `@/lib/api/homeLeave` which hits `POST /spaces/{spaceId}/groups/{groupId}/home-leave-preview`.
- Validates requirements 6.1 (trigger on change), 6.2 (500ms debounce), 6.3 (loading state), 6.7 (ignore stale responses).

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

The hook is a client-side React hook — full verification requires integration with the slider component (task 8.4).

## What comes next

- Task 8.3: `ImpactSummary` component that displays the preview data returned by this hook.
- Task 8.4: Integration of slider + hook + impact summary into the home-leave config panel.

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-slider): add useHomeLeavePreview hook with debounce and cancellation"
```
