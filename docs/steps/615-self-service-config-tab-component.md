# 615 — Self-Service Config Tab Component

## Phase

Phase — Self-Service Scheduling UI (Admin Tabs)

## Purpose

Provides group admins with a settings panel to configure self-service scheduling parameters (min/max shifts per cycle, request window offsets, cancellation cutoff, waitlist offer duration, and cycle duration). This replaces direct API calls with a user-friendly form that validates input client-side before submission.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/groups/selfService/SelfServiceConfigTab.tsx` | Admin config tab component with number inputs, client-side validation, loading/error/success states |

## Key decisions

- **Same pattern as MyShiftsTab**: Uses `useState` + `useCallback` for data fetching, local form state, and mutation handling — no global store needed.
- **HTML min/max constraints**: Each number input has `min` and `max` attributes matching the validation ranges for immediate browser-level feedback.
- **Validation before API call**: Uses `validateSelfServiceConfig` from the shared validation utility to catch errors before network round-trip.
- **Preserves user input on error**: API errors are displayed inline without resetting form values, allowing the user to correct and retry.
- **Success confirmation**: Shows a green banner on successful save that clears on the next edit.

## How it connects

- Consumes `getSelfServiceConfig` and `updateSelfServiceConfig` from `lib/api/selfService.ts`
- Uses `validateSelfServiceConfig` from `lib/utils/selfServiceValidation.ts`
- Uses `getSelfServiceErrorMessage` from `lib/utils/selfServiceErrors.ts`
- Uses i18n keys from `selfService.config.*` namespace in `messages/he.json`
- Will be rendered by the Group Detail Page when the admin navigates to the "self-service-config" tab

## How to run / verify

1. Navigate to a self-service group as an admin
2. Click the "הגדרות שירות עצמי" tab
3. Verify loading skeleton appears, then form with current values
4. Change a value to an invalid range (e.g., min > max) and click save — validation error should appear
5. Enter valid values and save — success banner should appear
6. Simulate API error — error message should appear, form values preserved

## What comes next

- Task 12.1: AdminOverridesTab component
- Task 14: Group creation wizard extension with scheduling mode selection

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): implement SelfServiceConfigTab admin component"
```
