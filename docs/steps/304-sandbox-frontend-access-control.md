# 304 — Sandbox Frontend Access Control

## Phase

Feature: Draft Simulation Sandbox — Task 9.1

## Purpose

Ensures that only users with group owner / space owner permissions can access the simulation sandbox. Non-admin users cannot see the "Enter Simulation" button, and if they somehow reach the sandbox state (e.g., via programmatic store manipulation), they are immediately redirected to the forbidden page.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/sandbox/SandboxView.tsx` | Added access control guard that checks `adminGroupId` against the sandbox's `groupId`. If the user is not an admin for the group, the sandbox is exited and the user is redirected to `/error/forbidden`. |

## Key decisions

1. **No dedicated sandbox route** — The sandbox is rendered as a full-screen overlay within the group page, not a separate route. Access control is enforced at the component level rather than via route middleware.

2. **Dual guard approach** — The "Enter Simulation" button is already hidden inside the `{isAdmin && (...)}` footer block in `DraftScheduleModal.tsx`. The `SandboxView` component adds a second layer of defense: a `useEffect` that monitors admin state and redirects if the user loses admin privileges while the sandbox is active.

3. **Synchronous render guard** — In addition to the `useEffect` redirect, a synchronous `return null` prevents any sandbox content from flashing before the redirect completes.

4. **Redirect to existing forbidden page** — Reuses the existing `/error/forbidden` page rather than creating a custom access denied UI, maintaining consistency with the rest of the app.

## How it connects

- **DraftScheduleModal.tsx** — The `isAdmin` prop (derived from `adminGroupId === groupId` in the auth store) controls button visibility. This was already implemented in prior tasks.
- **SandboxView.tsx** — Now subscribes to both `sandboxStore.groupId` and `authStore.adminGroupId` to enforce access control at the overlay level.
- **Auth store** — `adminGroupId` is the source of truth for admin mode, scoped per group.
- **Backend** — The simulation and publish endpoints independently verify permissions via `IPermissionService`, providing server-side enforcement regardless of frontend state.

## How to run / verify

1. Log in as a non-admin user (or exit admin mode on the group page)
2. Verify the "Enter Simulation" button is not visible in the draft review modal
3. If you programmatically call `useSandboxStore.getState().enterSandbox(...)` while not in admin mode, the sandbox overlay should immediately close and redirect to `/error/forbidden`
4. If you enter the sandbox as admin and then exit admin mode (toggle the admin button), the sandbox should close and redirect

## What comes next

- Task 9.2: Reactive UI split rendering verification (ensuring settings panel and preview have independent state subscriptions)

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): frontend access control guard for simulation sandbox"
```
