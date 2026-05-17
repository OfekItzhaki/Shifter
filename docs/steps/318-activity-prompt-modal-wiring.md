# 318 — Wire ActivityPromptModal to adminSessionStore

## Phase

Admin Session Timeout — Frontend Integration Wiring

## Purpose

Connects the `ActivityPromptModal` component to the `adminSessionStore` Zustand store so that the inactivity prompt is rendered globally whenever the store signals `isPromptVisible = true`. This ensures that admins in elevated mode are prompted before their session times out, regardless of which page they are on.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/admin/AdminSessionGuard.tsx` | New component that subscribes to `isPromptVisible` from `adminSessionStore` and renders `ActivityPromptModal`. Wires "Yes" → `dismissPrompt('yes')` (resets timer) and "No" → `dismissPrompt('no')` (exits elevated mode). |
| `apps/web/app/providers.tsx` | Added `AdminSessionGuard` to the global providers so the prompt modal is available app-wide. |

## Key decisions

- **Global placement in providers.tsx**: The `AdminSessionGuard` is rendered at the provider level (alongside `OfflineBanner`) so it's always mounted regardless of route. This matches the pattern used for other global UI elements.
- **Thin wrapper component**: `AdminSessionGuard` is intentionally minimal — it only reads `isPromptVisible` and `dismissPrompt` from the store and passes them to `ActivityPromptModal`. This keeps concerns separated and the component easy to test.
- **Stable callbacks via useCallback**: The `handleYes` and `handleNo` handlers are memoized to avoid unnecessary re-renders of the modal.

## How it connects

- **adminSessionStore** (`lib/store/adminSessionStore.ts`): The store's `showPrompt()` action sets `isPromptVisible = true`, which triggers the modal to render. `dismissPrompt('yes')` resets the timer; `dismissPrompt('no')` exits elevated mode.
- **ActivityPromptModal** (`components/admin/ActivityPromptModal.tsx`): Receives `open`, `countdownSeconds`, `onYes`, `onNo` props. Handles the 60-second countdown and auto-triggers `onNo` when it expires.
- **Task 9.4** (timeout exit behavior) will handle the redirect and toast notification when `exitElevatedMode` is called with reason `'prompt_no'`.
- **Task 9.5** (activity event listeners) will connect the inactivity timer that eventually calls `showPrompt()`.

## How to run / verify

1. The app compiles without errors (`npm run build` in `apps/web`).
2. When `adminSessionStore.showPrompt()` is called (e.g., from the inactivity timer), the `ActivityPromptModal` appears as a full-screen overlay.
3. Clicking "Yes" calls `dismissPrompt('yes')` — resets the timer and hides the modal.
4. Clicking "No" calls `dismissPrompt('no')` — exits elevated mode and hides the modal.
5. If the 60-second countdown expires, `onNo` is triggered automatically.

## What comes next

- **Task 9.4**: Wire timeout exit behavior (redirect + toast + backend event).
- **Task 9.5**: Wire activity event listeners and multi-tab sync that trigger `showPrompt()`.

## Git commit

```bash
git add -A && git commit -m "feat(admin-session): wire ActivityPromptModal to adminSessionStore"
```
