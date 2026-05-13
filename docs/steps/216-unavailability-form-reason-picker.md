# 216 — Unavailability Form Reason Picker

## Phase

Feature: Qualification Templates & Unavailability Reasons

## Purpose

Integrates the unavailability reason system into the presence window creation form. Admins can now select a predefined reason (or enter a custom one) when marking a member as unavailable, and existing presence windows display their associated reason.

## What was built

| File | Description |
|------|-------------|
| `apps/web/app/groups/[groupId]/tabs/MembersTab.tsx` | Updated `MemberProfileModal` to: (1) fetch reasons from API, (2) show a reason dropdown with predefined + "Custom" option, (3) wire reason selection to POST body, (4) display `reasonDisplayName` or `note` in presence window list |
| `apps/web/messages/en.json` | Added translation keys: `reason`, `selectReason`, `customReason`, `customReasonPlaceholder` |
| `apps/web/messages/he.json` | Added Hebrew translations for the new keys |
| `apps/web/messages/ru.json` | Added Russian translations for the new keys |

## Key decisions

- **Reason picker only shows when reasons exist**: If the space has no configured reasons, the dropdown is hidden and the form behaves exactly as before (backward compatible).
- **"Custom" maps to the existing `note` field**: When "Custom" is selected, the free-text input replaces the note field and its value is sent as `note` in the POST body. This reuses the existing backend field without requiring a new `customReason` field.
- **Predefined reason sends `reasonId`**: When a predefined reason is selected, the `reasonId` GUID is included in the POST body. The backend validates it belongs to the space.
- **Display priority**: In the presence window list, `reasonDisplayName` (from predefined reasons) takes priority over `note` (custom text). If neither exists, nothing is shown (handles legacy windows gracefully).
- **Max 200 chars for custom reason**: Enforced via `maxLength` attribute and `slice(0, 200)` on change.

## How it connects

- Uses `getReasons` from `@/lib/api/unavailabilityReasons` (created in task 6.1)
- Backend `AddPresenceRequest` already accepts `Guid? ReasonId` (task 3.2)
- Backend `GetPresenceQuery` already returns `reasonId` and `reasonDisplayName` via left-join (task 3.3)
- Unavailability reasons are seeded from templates (task 5.3) or managed via settings panel (task 6.2)

## How to run / verify

1. Open a group → Members tab → click a member → Availability tab
2. If the space has configured unavailability reasons, the "Reason" dropdown appears in the add form
3. Select a predefined reason → submit → the window shows the reason display name
4. Select "Custom" → enter text → submit → the window shows the custom text as note
5. Leave reason unselected → submit → backward compatible, no reason shown

## What comes next

- Property-based tests (task 10) for reason round-trip validation
- Final integration checkpoint (task 9)

## Git commit

```bash
git add -A && git commit -m "feat(unavailability): reason picker in presence form with display"
```
