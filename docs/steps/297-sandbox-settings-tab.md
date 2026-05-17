# 297 — Sandbox Settings Tab

## Phase

Phase — Draft Simulation Sandbox (Frontend)

## Purpose

Implement the Settings tab in the sandbox settings panel, allowing admins to override scheduling settings (minimum rest hours, home-leave parameters, minimum people at base, and qualification requirements) during a simulation session. All values are validated client-side before being included in the override payload.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/sandbox/SandboxSettingsTab.tsx` | New component implementing the full Settings tab with min rest hours slider/input, home-leave parameters section (conditional), min people at base (conditional), and qualification requirements editor |
| `apps/web/components/sandbox/SandboxSettingsPanel.tsx` | Updated to import and render `SandboxSettingsTab` instead of the placeholder `SettingsTabContent` |
| `apps/web/messages/en.json` | Added `sandbox.settings.*` translation keys for all Settings tab labels and descriptions |
| `apps/web/messages/he.json` | Added Hebrew translations for all Settings tab labels and descriptions |

## Key decisions

- **Slider + number input combo** for min rest hours: Matches the existing pattern in `SettingsTab.tsx` and provides both quick drag and precise input.
- **Conditional rendering** for home-leave and min-people-at-base sections: Only shown when `baseline.homeLeaveConfig?.enabled` is true, matching the requirement that these are only relevant for closed-base groups.
- **Client-side clamping validation**: All numeric inputs are clamped to their valid ranges (0–24 for rest hours, 1–max for capacity, 0–100 for balance) before calling `updateSettings`, satisfying Requirement 5.5.
- **Baseline value display**: Each overridden field shows the original baseline value for reference, with an amber highlight on overridden inputs and a reset button.
- **Qualification requirements editor**: Uses an expandable accordion pattern per task slot, allowing inline editing of count and mandatory/optional toggle for each qualification requirement.
- **Reusable `NumberField` component**: Extracted a generic number input component with baseline display, override highlighting, and reset functionality to reduce duplication across home-leave parameters.

## How it connects

- Reads from and writes to the `useSandboxStore` Zustand store via `settingsOverrides` state and `updateSettings` action (implemented in task 4.1).
- The qualification editor also uses `editTask` from the store to update task slot qualification requirements.
- The `buildOverridePayload` function (task 4.2) consumes `settingsOverrides` to inject/update the `min_rest_between_assignments` constraint and `HomeLeaveConfig` fields.
- Rendered inside `SandboxSettingsPanel` (task 6.2) as the "Settings" tab content.

## How to run / verify

1. Enter the simulation sandbox for a group with a draft version
2. Navigate to the "Settings" tab
3. Verify the min rest hours slider/input works (0–24 range, clamped)
4. For a closed-base group with home-leave enabled, verify home-leave parameter fields appear
5. Verify min people at base field appears for closed-base groups
6. Verify qualification requirements editor shows tasks with qualifications
7. Verify overridden values show amber highlight and baseline reference
8. Verify reset (✕) button clears the override

## What comes next

- Task 7.1: `SandboxSchedulePreview` component
- Task 7.2: Simulation run trigger (wires "Run Simulation" button)
- Task 12.2: Unit tests for settings validation

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): implement Settings tab in sandbox settings panel"
```
