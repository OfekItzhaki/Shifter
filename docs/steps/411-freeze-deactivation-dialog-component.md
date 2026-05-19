# 411 — Freeze Deactivation Dialog Component

## Phase

Feature — Freeze Period Discard (Task 7.2)

## Purpose

Provides the admin-facing confirmation dialog for deactivating an emergency freeze. The dialog fetches and displays categorized change counts, offers a discard toggle (permission-gated), and handles error/empty states gracefully.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/home-leave/FreezeDeactivationDialog.tsx` | New dialog component with change count display, discard toggle, loading/error states |
| `apps/web/messages/en.json` | Added `homeLeave.deactivationDialog` translation keys |
| `apps/web/messages/he.json` | Added `homeLeave.deactivationDialog` Hebrew translation keys |

## Key decisions

1. **Prop-based permission gating** — `canRollback` prop controls visibility of the discard toggle. The frontend has no granular permission store, so the parent component is responsible for determining this value.
2. **Inline dialog overlay** — Matches the project's existing pattern of custom dialogs (no external dialog library). Uses a fixed overlay with centered card.
3. **Separation of concerns** — The dialog only handles presentation and count fetching. The actual deactivation API call is delegated to the parent via `onConfirm(discardFreezeChanges)`.
4. **Default unchecked** — Discard toggle defaults to unchecked per Requirement 1.2.
5. **Hidden vs disabled** — Toggle is completely hidden when user lacks permission (Req 5.2) or no changes exist (Req 1.4). It's shown but disabled with an error message when the count fetch fails (Req 1.5).

## How it connects

- Consumes `getFreezePeriodChangesCount` from `lib/api/homeLeave.ts` (Task 7.1)
- Will be integrated into `EmergencyFreezeBanner` in Task 7.3
- Parent passes `canRollback` based on user's `schedule.rollback` permission

## How to run / verify

1. Import `FreezeDeactivationDialog` in a parent component
2. Pass `open={true}`, valid `spaceId`/`groupId`, and `canRollback` boolean
3. Verify: dialog fetches counts on open, displays categories, shows/hides toggle based on permission and count state
4. TypeScript compilation: `npx tsc --noEmit` in `apps/web`

## What comes next

- Task 7.3: Integrate dialog into `EmergencyFreezeBanner` component
- Task 7.4: Unit tests for the dialog

## Git commit

```bash
git add -A && git commit -m "feat(freeze-discard): add FreezeDeactivationDialog component"
```
