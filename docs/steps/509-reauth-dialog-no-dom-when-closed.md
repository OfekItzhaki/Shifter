# 509 — ReAuth Dialog Renders No DOM When Closed

## Phase

Admin Re-Auth Security — Frontend Autofill Prevention and Password Form Hardening

## Purpose

Verify that the `ReAuthDialog` component renders absolutely no DOM elements (no inputs, forms, or overlays) when the `open` prop is `false`. This ensures autofill-prevention attributes cannot be discovered or triggered by password managers when the dialog is not visible, satisfying Requirement 5.3.

## What was verified

| File | Status | Notes |
|------|--------|-------|
| `apps/web/components/admin/ReAuthDialog.tsx` (line 430) | ✅ Correct | `if (!open) return null;` early-returns before any JSX |
| No `createPortal` usage | ✅ Confirmed | No portals that could render DOM outside the conditional |
| No imperative DOM manipulation | ✅ Confirmed | No `document.createElement`, `appendChild`, or similar |
| All `useEffect` hooks | ✅ Correct | Either return early when `!open` or only manage internal state/cleanup |

## Key decisions

- **No code changes required** — the existing implementation already satisfies Requirement 5.3 completely.
- The early `return null` pattern is the standard React approach for conditional rendering and is the most reliable way to ensure zero DOM output.
- Effects that depend on `open` (credential fetch, focus management, state reset) all guard against running when `open` is `false`.

## How it connects

- **Requirement 5.3**: "WHILE the ReAuth_Dialog is not open, THE ReAuth_Dialog SHALL not render any input elements or form elements in the DOM."
- This behavior ensures password managers cannot detect or interact with the re-auth form fields when the dialog is closed, complementing the autofill-prevention attributes applied in task 4.1.
- Property test 4.6 will formally verify this property across arbitrary prop combinations.

## How to verify

The behavior can be confirmed by:
1. Inspecting `ReAuthDialog.tsx` line 430: `if (!open) return null;`
2. Searching for `createPortal` or imperative DOM APIs — none exist in the file
3. The upcoming property test (task 4.6) will use fast-check to verify this across all prop combinations

## What comes next

- Task 4.4: Property test for dialog state reset on open
- Task 4.5: Property test for whitespace password rejection
- Task 4.6: Property test for no DOM rendering when closed (formal verification of this behavior)

## Git commit

```bash
git add -A && git commit -m "docs(admin-reauth): verify dialog renders no DOM when closed"
```
