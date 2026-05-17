# 317 — Activity Prompt Modal Component

## Phase

Admin Session Timeout — Frontend Activity Prompt

## Purpose

Implements the `ActivityPromptModal` component that displays when the inactivity timer expires during an elevated session (Management Mode or Super Platform Mode). The modal asks "Are you still active?" with a visible countdown timer, giving the admin a chance to continue their session before automatic exit.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/admin/ActivityPromptModal.tsx` | Modal component with countdown timer, Yes/No buttons, focus trap, and auto-exit on countdown expiry |
| `apps/web/__tests__/session/activityPromptModal.test.tsx` | Unit tests covering rendering, countdown, focus management, keyboard navigation, and auto-exit |
| `apps/web/messages/en.json` | Added `activityPrompt` translation keys (English) |
| `apps/web/messages/he.json` | Added `activityPrompt` translation keys (Hebrew) |

## Key decisions

- **`alertdialog` role**: Used `role="alertdialog"` instead of `role="dialog"` since this is an urgent prompt requiring immediate user attention.
- **Focus trap**: Implemented manually (matching the existing `ReAuthDialog` pattern) rather than adding a library dependency.
- **Countdown color change**: Timer text turns red when ≤10 seconds remain for visual urgency.
- **No Escape key dismiss**: Unlike `ReAuthDialog`, the activity prompt does not dismiss on Escape — the user must explicitly choose Yes or No (or let the countdown expire).
- **RTL direction**: Follows the existing modal pattern with `direction: "rtl"` for Hebrew-first UI.

## How it connects

- **Used by**: Task 9.3 will wire this component to the `adminSessionStore`, rendering it when `isPromptVisible` becomes true.
- **Depends on**: `next-intl` for translations, standard React hooks.
- **Follows pattern of**: `ReAuthDialog` component for styling, overlay, and focus trap implementation.

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/session/activityPromptModal.test.tsx
```

All 12 tests should pass.

## What comes next

- Task 9.3: Wire `ActivityPromptModal` to `adminSessionStore` (render when `isPromptVisible` is true, handle Yes/No responses).

## Git commit

```bash
git add -A && git commit -m "feat(session): add ActivityPromptModal component with countdown and focus trap"
```
