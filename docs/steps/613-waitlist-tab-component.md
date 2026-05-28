# 613 — Waitlist Tab Component

## Phase

Self-Service Scheduling UI — Member Tab Components

## Purpose

Implements the WaitlistTab component that allows group members to view their waitlist entries, respond to slot offers (accept/decline), and leave waitlists. This is the member-facing UI for the waitlist feature of the self-service scheduling system.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/groups/[groupId]/tabs/WaitlistTab.tsx` | Self-contained tab component with full state management for waitlist entries |

## Key decisions

- **Self-contained state management**: The component manages its own state via `useState` + `useEffect`, consistent with the `LiveStatusPanel` pattern. No new zustand store needed.
- **Offered entries highlighted**: Entries with status "Offered" are rendered in a separate section with a prominent sky-blue border and background, countdown timer, and Accept/Decline buttons.
- **Countdown refresh**: A 30-second interval refreshes the countdown display for offered entries.
- **Decline via leaveWaitlist**: The "Decline" action calls `leaveWaitlist` on the offered slot, which the backend handles as a decline.
- **Leave confirmation**: A Modal dialog confirms before leaving a waitlist entry with "Waiting" status.
- **Error handling**: Uses `getSelfServiceErrorMessage` from the error mapping utility for consistent Hebrew error display.
- **Loading skeleton**: Shows 3 animated placeholder cards during initial load.
- **Retry pattern**: Error state shows a "נסה שוב" (Retry) button that re-triggers the fetch.

## How it connects

- Consumes API functions from `lib/api/selfService.ts` (task 1.1)
- Uses formatting utilities from `lib/utils/selfServiceFormat.ts` (task 2.3)
- Uses error mapping from `lib/utils/selfServiceErrors.ts` (task 3.2)
- Uses i18n keys from `selfService.waitlist.*` namespace (task 3.1)
- Uses the shared `Modal` component for the leave confirmation dialog
- Will be lazy-loaded by the GroupDetailPage tab navigation (task 15.1)

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

The component compiles cleanly with no TypeScript errors.

## What comes next

- Task 7.2: Property test for waitlist display logic (Property 9)
- Task 15.1: Wire the WaitlistTab into the GroupDetailPage tab navigation
- Task 16.1: Shared loading/error components (already implemented inline here)

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): implement WaitlistTab component"
```
