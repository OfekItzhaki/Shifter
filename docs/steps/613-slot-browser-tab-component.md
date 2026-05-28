# 613 — Slot Browser Tab Component

## Phase

Self-Service Scheduling UI — Member Tab Components

## Purpose

Implements the `SlotBrowserTab` component that allows group members to browse available shift slots for the current scheduling cycle, request shifts, and join waitlists for full slots. This is the primary member-facing interface for the self-service scheduling flow.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/groups/[groupId]/tabs/SlotBrowserTab.tsx` | Full slot browser tab component with data fetching, sorting, filtering, request/waitlist actions, and loading/error states |

## Key decisions

- **Placed in existing tabs directory** — follows the same pattern as `StatsTab`, `AlertsTab`, etc. under `app/groups/[groupId]/tabs/`
- **Local state management** — uses `useState` + `useEffect` for data fetching, consistent with other tabs (no global store needed)
- **Optimistic capacity update** — on successful shift request, the local slot's `currentFillCount` is incremented immediately without a full refetch
- **Date filter as buttons** — uses the same button-style filter pattern seen in the stats tab time range selector
- **Capacity class via utility** — delegates visual classification to `getCapacityClass` from `selfServiceFormat.ts`
- **Error handling via utility** — uses `getSelfServiceErrorMessage` from `selfServiceErrors.ts` for consistent error display
- **Inline error per slot** — errors from request/waitlist actions are shown below the specific slot that triggered them

## How it connects

- Consumes API functions from `lib/api/selfService.ts` (task 1.1)
- Uses formatting utilities from `lib/utils/selfServiceFormat.ts` (task 2.3)
- Uses error mapping from `lib/utils/selfServiceErrors.ts` (task 3.2)
- Uses i18n keys from `selfService.slotBrowser.*` namespace (task 3.1)
- Will be lazy-loaded by the group detail page in the tab navigation task (task 15.1)

## How to run / verify

- TypeScript compilation: `npx tsc --noEmit` in the `apps/web` directory
- The component will be visible once wired into the group detail page tab navigation (task 15.1)
- Property tests for display logic will be added in task 5.2

## What comes next

- Task 5.2: Property tests for slot browser display logic
- Task 15.1: Wire the tab into the group detail page's mode-conditional tab navigation
- Task 16.1: Shared loading/error components (this tab already implements inline versions)

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): implement SlotBrowserTab component"
```
