# 379 — Pricing Page Back Link History Navigation

## Phase

Bugfix — Redirect and Member Email Fix (Task 3.1)

## Purpose

The pricing page's "back" link was hardcoded to navigate to `/login`, which is incorrect when users arrive from other pages (e.g., `/groups`). This fix replaces the hardcoded `<Link href="/login">` with a button that uses browser history navigation (`router.back()`), with a fallback to `/` if there is no history.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/pricing/page.tsx` | Removed `<Link>` import, added `useRouter` from `next/navigation`. Replaced `<Link href="/login">` with a `<button>` that calls `handleBack()`. Added `handleBack` function that checks `window.history.length` and either calls `router.back()` or `router.push("/")` as fallback. |

## Key decisions

- Used a `<button>` element instead of `<Link>` since the navigation target is dynamic (history-based), not a static route.
- Styled the button to look identical to the previous link (no border, no background, same color and font size).
- Fallback condition: `window.history.length <= 1` triggers navigation to `/` instead of `router.back()`, preventing users from being stuck on the pricing page with no history.
- Added `typeof window !== "undefined"` guard for SSR safety.

## How it connects

- Fixes bug condition `isBugCondition(input) where input.type = "pricing_back_click"` from the spec.
- Preserves requirement 3.1: pricing page continues to render without requiring authentication.
- The page remains a `"use client"` component (already was), so `useRouter` works correctly.

## How to run / verify

1. Navigate to the pricing page from any page (e.g., `/groups`).
2. Click the "← Back" button.
3. Verify you return to the previous page, not `/login`.
4. Open the pricing page directly (no history) — clicking back should navigate to `/`.

## What comes next

- Task 3.6 will re-run the bug condition exploration test to confirm this fix passes.
- Task 3.7 will verify preservation tests still pass.

## Git commit

```bash
git add -A && git commit -m "fix(pricing): replace hardcoded /login back link with history navigation"
```
