# 614 — Swaps Tab Component

## Phase
Self-Service Scheduling UI — Member Tab Components

## Purpose
Implements the SwapsTab component that allows group members to view, propose, accept, decline, and cancel shift swap requests with other members.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/groups/[groupId]/tabs/SwapsTab.tsx` | Full SwapsTab component with swap listing, propose flow, accept/decline/cancel actions, countdown timer, loading/error states |

## Key decisions

1. **Swap classification by direction**: Swaps are classified as incoming (target is current user), outgoing (initiator is current user), or completed (non-pending status) for clear UI grouping.
2. **Person ID resolution**: The current user's `personId` is resolved by matching `linkedUserId` from the members list against the auth store's `userId`.
3. **Multi-step propose flow**: The "Propose Swap" flow uses a step-based state machine (`idle` → `selectMyShift` → `selectTargetMember` → `selectTargetShift`) for progressive disclosure.
4. **Error handling via `getSelfServiceErrorMessage`**: All API errors are processed through the shared error mapping utility for consistent Hebrew error display.
5. **Countdown timer**: Uses the existing `formatCountdown` utility from `selfServiceFormat.ts` to display remaining time for pending swaps with 72h expiry.
6. **Sub-component extraction**: `SwapCard` and `StatusBadge` are extracted as internal sub-components for readability and reuse within the file.

## How it connects

- Uses API functions from `lib/api/selfService.ts` (`getMySwaps`, `proposeSwap`, `acceptSwap`, `declineSwap`, `cancelSwap`, `getMyShiftRequests`)
- Uses formatting utilities from `lib/utils/selfServiceFormat.ts` (`formatSlotDate`, `formatTime24h`, `formatCountdown`)
- Uses error mapping from `lib/utils/selfServiceErrors.ts` (`getSelfServiceErrorMessage`)
- Uses i18n keys from `selfService.swaps.*` namespace
- Receives `members: GroupMemberDto[]` prop from the parent group page for the member picker in the propose flow
- Follows the same tab component pattern as `StatsTab.tsx`, `AlertsTab.tsx`, etc.

## How to run / verify

1. Navigate to a self-service group's "swaps" tab
2. Verify loading skeleton appears during fetch
3. Verify swap cards display status, counterpart name, offered/requested shift details
4. Verify countdown timer shows for pending swaps
5. Verify "Propose Swap" flow walks through shift selection → member selection → target shift selection
6. Verify Accept/Decline buttons appear on incoming proposals
7. Verify Cancel button appears on outgoing pending swaps
8. Verify error state shows retry button on fetch failure

## What comes next

- Task 8.2: Property tests for swap display logic (Property 10)
- Task 15.1: Wire SwapsTab into the mode-conditional tab navigation on the group detail page

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): implement SwapsTab component with propose/accept/decline/cancel flows"
```
