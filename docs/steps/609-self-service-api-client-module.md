# 609 — Self-Service API Client Module

## Phase

Self-Service Scheduling UI — Foundation Layer

## Purpose

Provides a typed API client module for all self-service scheduling endpoints. This is the data access layer that all self-service UI components will use to communicate with the backend.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/selfService.ts` | Complete API client module with all TypeScript interfaces and async functions for self-service scheduling |

### Interfaces defined

- `ShiftTemplateDto`, `CreateShiftTemplatePayload`, `UpdateShiftTemplatePayload`
- `SelfServiceConfigDto`, `UpdateSelfServiceConfigPayload`
- `AvailableSlotDto`, `AvailableSlotsResponse`
- `ShiftRequestDto`, `MyShiftsResponse`
- `WaitlistEntryDto`
- `SwapRequestDto`

### API functions implemented

- **Shift Templates**: `listShiftTemplates`, `createShiftTemplate`, `updateShiftTemplate`, `deleteShiftTemplate`
- **Self-Service Config**: `getSelfServiceConfig`, `updateSelfServiceConfig`
- **Available Slots**: `getAvailableSlots`
- **Shift Requests**: `submitShiftRequest`, `cancelShiftRequest`, `getMyShiftRequests`
- **Waitlist**: `joinWaitlist`, `leaveWaitlist`, `acceptWaitlistOffer`, `getMyWaitlistEntries`
- **Shift Swaps**: `proposeSwap`, `acceptSwap`, `declineSwap`, `cancelSwap`, `getMySwaps`
- **Admin Overrides**: `adminAssignMember`, `adminRemoveMember`

## Key decisions

1. **Follows existing `apiClient` pattern** — uses the same `apiClient` axios instance from `./client`, same async/await style, same return patterns as `groups.ts` and `schedule.ts`.
2. **All URLs include spaceId and groupId** — consistent with the backend route pattern `/spaces/{spaceId}/groups/{groupId}/...`.
3. **Query params via axios `params` option** — for `getAvailableSlots` (cycleId) and `getMyShiftRequests` (optional schedulingCycleId).
4. **Error propagation** — errors are not caught; they propagate to the calling component so UI can extract and display specific error messages (Requirement 11.4).

## How it connects

- Used by all self-service tab components (SlotBrowserTab, MyShiftsTab, WaitlistTab, SwapsTab, ShiftTemplatesTab, SelfServiceConfigTab, AdminOverridesTab)
- Depends on `lib/api/client.ts` (apiClient instance with auth interceptors)
- Backend controllers: `ShiftTemplatesController`, `SelfServiceConfigController`, `ShiftSlotsController`, `ShiftRequestsController`, `WaitlistController`, `ShiftSwapsController`, `AdminShiftOverridesController`

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

The file should compile without errors. No runtime verification needed — this is a pure API client module.

## What comes next

- Task 1.2: Property test for API client URL construction (Property 15)
- Task 2.1: Validation utilities (`selfServiceValidation.ts`)
- Task 2.3: Formatting utilities (`selfServiceFormat.ts`)

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): add typed API client module for self-service scheduling"
```
