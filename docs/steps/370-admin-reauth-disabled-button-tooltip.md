# 370 — Admin Reauth Disabled Button with Tooltip

## Phase

Feature — Admin Re-Authentication Gate (Task 3.3)

## Purpose

When a user has no credentials configured (`hasCredentials === false`), the admin mode toggle button must be visually disabled with a tooltip explaining that credentials must be configured first. This prevents confusion and provides clear guidance on why the button is non-functional.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/page.tsx` | Enhanced the admin mode toggle button with: a proper CSS tooltip (replacing native `title` attribute), `opacity-60` for stronger visual disabled feedback, `aria-describedby` for accessibility, and switched tooltip text to use `reAuth.noCredentials` translation key. Wrapped button in a relative container with `group/admin-btn` for hover-triggered tooltip display. |

## Key decisions

- **Custom CSS tooltip over native `title`**: The native `title` attribute doesn't show on mobile devices and has inconsistent screen reader support. A custom tooltip with `role="tooltip"` and `aria-describedby` provides better accessibility and UX.
- **Used `reAuth.noCredentials` key**: This key already exists in all three locales (en, he, ru) with a more descriptive message than the shorter `groups.noCredentialsTooltip` key.
- **Added `opacity-60`**: Combined with the existing `text-slate-400` and `cursor-not-allowed`, this makes the disabled state unmistakably different from the normal state.
- **Tooltip positioned above button**: Uses `bottom-full` positioning with a caret arrow pointing down, appearing on hover via Tailwind group-hover utility.

## How it connects

- Satisfies Requirements 2.7 (no admin mode entry without credentials) and 1.4 (cancel elevation when not authenticated)
- Works with the existing `hasCredentials` state and `handleAdminModeToggle` guard
- Uses the `reAuth` translation namespace already used by `ReAuthDialog`
- The tooltip is only rendered when `hasCredentials === false`, keeping the DOM clean in the normal case

## How to run / verify

1. Start the dev server: `npm run dev` in `apps/web`
2. Navigate to a group detail page
3. To simulate no credentials: temporarily modify the `checkCredentials` function to set `setHasCredentials(false)`
4. Verify: button appears faded (opacity-60, slate colors, cursor-not-allowed)
5. Verify: hovering shows a tooltip with the "no credentials" message
6. Verify: clicking the button does nothing (disabled attribute + guard)
7. Verify: the tooltip text matches the user's locale

## What comes next

- Task 3.4: Verify ReAuthDialog accessibility compliance
- Task 3.5: Verify loading and submission state handling

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): add disabled button tooltip for no-credentials state"
```
