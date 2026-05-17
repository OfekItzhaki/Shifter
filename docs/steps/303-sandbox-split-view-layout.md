# Step 303 — Sandbox Split View Layout

## Phase

Phase 4 — Draft Simulation Sandbox (Frontend UI)

## Purpose

Creates the `SandboxView` container component that renders the settings panel and schedule preview as independent React components in a side-by-side split layout. This ensures reactive UI boundaries are maintained: the settings panel subscribes only to override state, the schedule preview subscribes only to simulation results, and neither causes the other to re-render.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/sandbox/SandboxView.tsx` | New split-view container that renders `SandboxSettingsPanel` (left, 420px) and `SandboxSchedulePreview` (right, flex) as a full-screen overlay when `isActive === true` |
| `apps/web/app/groups/[groupId]/page.tsx` | Added import and render of `<SandboxView />` — shows when sandbox store's `isActive` is true |
| `apps/web/messages/en.json` | Added `sandbox.title` and `sandbox.sandboxBadge` translation keys |
| `apps/web/messages/he.json` | Added Hebrew translations for the new keys |
| `apps/web/messages/ru.json` | Added Russian translations for the new keys |

## Key decisions

1. **Fixed overlay approach**: The `SandboxView` renders as a `fixed inset-0 z-50` overlay rather than replacing page content inline. This avoids complex conditional rendering in the group page and ensures the sandbox takes full screen real estate for the split view.

2. **Minimal store subscription in container**: `SandboxView` subscribes only to `isActive` from the sandbox store. It does not subscribe to override state or simulation results, so it never re-renders when the user modifies parameters or when simulation completes.

3. **Independent child components**: `SandboxSettingsPanel` and `SandboxSchedulePreview` are rendered as direct children with their own independent Zustand subscriptions. The settings panel subscribes to `taskOverrides`, `constraintOverrides`, `memberExclusions`, `settingsOverrides`, `baseline`, and `isSimulating`. The schedule preview subscribes to `lastSimulationResult`, `isSimulating`, `simulationError`, and `baseline`. There is no shared subscription that would cause cross-panel re-renders.

4. **Left panel fixed width**: The settings panel is 420px wide (shrink-0) to provide enough space for form controls, while the schedule preview takes the remaining space (flex-1).

## How it connects

- **Sandbox entry**: `DraftScheduleModal` calls `enterSandbox()` which sets `isActive = true`, causing `SandboxView` to appear.
- **Sandbox exit**: Both publish and discard flows call `exitSandbox()` which sets `isActive = false`, causing `SandboxView` to disappear.
- **Settings panel**: Already implemented in task 6.2, subscribes only to override state.
- **Schedule preview**: Already implemented in task 7.1, subscribes only to simulation results.

## How to run / verify

1. Enter a group with a draft version
2. Click "Enter Simulation" in the draft modal
3. Verify the split view appears with settings panel on the left and preview on the right
4. Modify parameters in the settings panel — verify the preview does NOT re-render
5. Run a simulation — verify the settings panel does NOT re-render or lose scroll position
6. Publish or discard — verify the overlay disappears

## What comes next

- Task 9.1 (frontend access control) ensures only admins can enter the sandbox
- Task 10 (checkpoint) verifies full integration

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): add split-view layout with reactive UI boundaries"
```
