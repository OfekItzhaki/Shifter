# 543 — Invite Code Card Component

## Phase

Phase — Space Management Frontend

## Purpose

Extracts the inline invite code section from the space settings page into a reusable, permission-gated `InviteCodeCard` component. This improves code organization, makes the component independently testable, and adds a proper confirmation dialog before regenerating the invite code (instead of a browser `confirm()` prompt).

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/spaces/InviteCodeCard.tsx` | New component that displays the current invite code, provides a copy-to-clipboard button, and a regenerate button with inline confirmation dialog. Only renders when `isOwner` is true and an invite code exists. |
| `apps/web/app/spaces/settings/page.tsx` | Updated to import and use `InviteCodeCard` instead of the inline invite code section. Removed unused `regenerateInviteCode` import, `copied` state, `handleRegenerate`, and `handleCopy` callbacks. |

## Key decisions

1. **Inline confirmation instead of `window.confirm()`** — The previous implementation used `confirm()` which is not styleable and blocks the thread. The new component uses an inline confirmation UI with explicit confirm/cancel buttons.
2. **Uses `regenerateSpaceInviteCode` API** — The component calls the newer `regenerateSpaceInviteCode` function (from task 13.1) rather than the older `regenerateInviteCode` function.
3. **Local state for code** — The component maintains its own `currentCode` state initialized from props, and updates it after successful regeneration. It also calls `onCodeRegenerated` to sync the parent.
4. **Permission gating via props** — The component accepts `isOwner` as a prop and returns `null` if the user is not the owner, consistent with the `SpaceBillingCard` pattern.

## How it connects

- Depends on `regenerateSpaceInviteCode` from `apps/web/lib/api/spaces.ts` (task 13.1)
- Used by the space settings page at `apps/web/app/spaces/settings/page.tsx`
- The `inviteCode` value comes from `getSpaceDetail` which returns `SpaceDetailDto.inviteCode`
- Translation keys used: `spaces.inviteCode`, `spaces.copyCode`, `spaces.copied`, `spaces.regenerateCode`, `spaces.regenerateConfirm`

## How to run / verify

1. Navigate to `/spaces/settings` as a space owner
2. Verify the invite code card displays the current code
3. Click "Copy" — code should be copied to clipboard, button text changes to "Copied!"
4. Click "Generate new code" — inline confirmation appears
5. Click "Cancel" — confirmation disappears
6. Click "Generate new code" again, then confirm — API is called, new code is displayed
7. As a non-owner, verify the card does not render

## What comes next

- Task 18.2: Unit tests for the invite code section (Vitest + React Testing Library)

## Git commit

```bash
git add -A && git commit -m "feat(space-management): extract InviteCodeCard component with confirmation dialog"
```
