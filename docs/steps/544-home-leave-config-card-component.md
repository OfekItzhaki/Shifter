# Step 544 — HomeLeaveConfigCard Component

## Phase

Phase — Space Management Frontend

## Purpose

Implements the space-level home-leave configuration card for the space settings page. This component allows the Space Owner to configure home-leave scheduling parameters (mode, balance ratio, manual fields, emergency freeze) that apply to all closed-base groups in the space.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/spaces/HomeLeaveConfigCard.tsx` | New component with mode selector (Automatic/Manual/Disabled), ratio slider, manual mode fields, emergency freeze toggle, and save functionality |
| `apps/web/app/spaces/settings/page.tsx` | Integrated `HomeLeaveConfigCard` into the space settings page |
| `apps/web/lib/api/spaces.ts` | Updated `SpaceHomeLeaveMode` type to include `"disabled"` option |
| `apps/web/messages/en.json` | Added `spaceHomeLeave` translation keys (English) |
| `apps/web/messages/he.json` | Added `spaceHomeLeave` translation keys (Hebrew) |
| `apps/web/messages/ru.json` | Added `spaceHomeLeave` translation keys (Russian) |

## Key decisions

1. **Component placement**: Created under `components/spaces/` directory to establish a pattern for space-level settings components, separate from the group-level `components/home-leave/` directory.
2. **Reused patterns from SpaceBillingCard**: Permission gating via `isOwner` prop, loading/error states, card styling with rounded-2xl and shadow-sm.
3. **Added "disabled" mode**: The task specifies three modes (Automatic/Manual/Disabled). Extended the `SpaceHomeLeaveMode` type to include `"disabled"` since the backend enum only had Automatic and Manual.
4. **Self-contained component**: Unlike the group-level `HomeLeaveConfigPanel` which uses sub-components (ModeSelector, RatioSlider, ManualModeSection), this card is self-contained for simplicity since it's a space-level overview without feasibility checks.
5. **Emergency freeze toggle**: Implemented as a switch with a conditional "use for scheduling" checkbox sub-option that appears when freeze is active.

## How it connects

- Consumes `getHomeLeaveConfig` and `updateHomeLeaveConfig` from `lib/api/spaces.ts` (task 13.1)
- Rendered on the space settings page (`/spaces/settings`) alongside other owner-only cards
- Mirrors the group-level `HomeLeaveConfigPanel` but operates at the space level per Requirement 6
- The space-level config overrides group-level values in the solver payload (backend task 10.1)

## How to run / verify

1. Navigate to `/spaces/settings` as a space owner
2. The "Home Leave Configuration" card should appear with mode selector, slider/fields, and freeze toggle
3. Switching modes should show/hide the appropriate fields
4. Saving should call the `PUT /spaces/{spaceId}/home-leave-config` endpoint
5. Non-owners should not see the card at all

## What comes next

- Task 15.2: Unit tests for HomeLeaveConfigCard (Vitest + React Testing Library)
- Task 16.1: DangerZoneCard component
- Task 17.1: RoleAssignmentCard component

## Git commit

```bash
git add -A && git commit -m "feat(space-management): HomeLeaveConfigCard component for space settings"
```
