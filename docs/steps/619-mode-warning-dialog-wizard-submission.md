# Step 619 — ModeWarningDialog and Wizard Submission

## Phase

Self-Service Scheduling UI — Group Creation Wizard Extension

## Purpose

Adds a confirmation dialog between the template selection step and the actual group creation API call. This ensures users explicitly acknowledge that their scheduling mode choice is permanent and irreversible before the group is created.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/groups/selfService/ModeWarningDialog.tsx` | New confirmation dialog component with irreversibility warning, selected mode badge, and confirm/cancel buttons |
| `apps/web/components/CreateGroupWizard.tsx` | Extended to show `ModeWarningDialog` when user clicks "continue" on step 3, instead of directly calling the API |

## Key decisions

- **Dialog uses existing `Modal` component** — consistent with the rest of the app's modal pattern
- **Error handling preserves selections** — on API error, the dialog closes but the wizard stays on step 3 with all selections intact, allowing retry
- **`isPending` prop passed to dialog** — disables buttons and shows spinner during API call to prevent double-submission
- **i18n keys from `selfService.confirmDialog.*`** — uses the already-defined Hebrew/English translations
- **Selected mode displayed as badge** — gives the user a clear visual of what they're confirming

## How it connects

- Depends on: `SchedulingModeSelector` (task 14.1), `Modal` component, i18n messages (task 3.1)
- Used by: The group creation flow — any page that renders `CreateGroupWizard`
- Satisfies: Requirements 1.8 (confirmation dialog), 1.9 (API call with schedulingMode), 1.10 (error handling with retry)

## How to run / verify

1. Open the group creation wizard
2. Enter a group name → Continue
3. Select a scheduling mode → Continue
4. Select a template → Click "Continue"
5. The `ModeWarningDialog` should appear with the irreversibility warning
6. Click "Cancel" → dialog closes, wizard stays on step 3
7. Click "Continue" again → dialog reappears
8. Click "Confirm" → API is called, group is created

## What comes next

- Final integration checkpoint (task 17)

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): add ModeWarningDialog and wizard submission confirmation"
```
