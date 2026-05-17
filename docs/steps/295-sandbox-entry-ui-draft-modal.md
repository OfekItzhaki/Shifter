# 295 — Sandbox Entry UI in Draft Review Panel

## Phase

Phase — Draft Simulation Sandbox (Frontend)

## Purpose

Adds the "Enter Simulation" button to the existing `DraftScheduleModal` component, allowing admins to enter the simulation sandbox directly from the draft review panel. The button fetches the solver baseline from the backend and initializes the sandbox Zustand store.

## What was built

| File | Change |
|------|--------|
| `apps/web/components/DraftScheduleModal.tsx` | Added `groupId` prop, imported `useSandboxStore`, added `handleEnterSimulation` handler that fetches solver baseline and calls `enterSandbox`, added purple "Enter Simulation" button in the footer |
| `apps/web/app/groups/[groupId]/page.tsx` | Passes `groupId` prop to `DraftScheduleModal` |
| `apps/web/messages/en.json` | Added `enterSimulation`, `enteringSimulation`, `errorEnterSimulation` keys to `draftModal` |
| `apps/web/messages/he.json` | Added Hebrew translations for the new keys |
| `apps/web/messages/ru.json` | Added Russian translations for the new keys |

## Key decisions

- **Button placement**: The "Enter Simulation" button is placed between "Publish" and "Run Again" in the footer, using a purple (`#8b5cf6`) color to visually distinguish it from the other actions.
- **Conditional visibility**: The button only appears when `isAdmin` is true (same guard as the entire footer). The modal itself is only rendered when a `draftVersion` exists, satisfying the requirement to hide the button when no draft exists.
- **Loading state**: A dedicated `enteringSimulation` state prevents double-clicks and shows a loading indicator while the baseline is being fetched.
- **Error handling**: If the baseline fetch fails, the error is displayed inline in the modal footer without closing the modal, preserving the user's context.

## How it connects

- Calls `GET /spaces/{spaceId}/groups/{groupId}/solver-baseline` (implemented in step 290)
- Calls `enterSandbox` action from `useSandboxStore` (implemented in step 291)
- The sandbox settings panel (task 6.2) will read from the store initialized here

## How to run / verify

1. Navigate to a group with a draft schedule version
2. Open the draft review modal
3. Verify the purple "🧪 Enter Simulation" button appears in the footer (admin only)
4. Click the button — it should fetch the baseline and activate the sandbox store
5. Verify the modal closes after successful entry

## What comes next

- Task 6.2: `SandboxSettingsPanel` component that renders the sandbox UI once the store is active
- Task 9.1: Frontend access control to hide the button for non-admin users

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): add Enter Simulation button to draft review modal"
```
