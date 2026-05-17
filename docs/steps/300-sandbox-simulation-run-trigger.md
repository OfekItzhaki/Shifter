# 300 — Sandbox Simulation Run Trigger

## Phase

Phase 7 — Draft Simulation Sandbox (Frontend — Schedule Preview & Simulation Execution)

## Purpose

Wires up the "Run Simulation" button in the `SandboxSettingsPanel` to build the override payload from the current sandbox state and POST it to the simulation endpoint. Handles success, solver timeout, infeasibility, and network error responses with localized messages.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/simulation.ts` | New API module with `runSimulation` function that POSTs to `/spaces/{spaceId}/groups/{groupId}/simulate` |
| `apps/web/components/sandbox/SandboxSettingsPanel.tsx` | Updated `handleRunSimulation` callback to: build payload, call API, handle all response cases |
| `apps/web/messages/en.json` | Added `sandbox.errors.solverTimeout`, `sandbox.errors.networkError`, `sandbox.errors.infeasible` keys |
| `apps/web/messages/he.json` | Added Hebrew translations for simulation error messages |
| `apps/web/messages/ru.json` | Added Russian translations for simulation error messages |

## Key decisions

1. **Direct store access via `getState()`** — The handler uses `useSandboxStore.getState()` and `useSpaceStore.getState()` to read the latest state at call time, avoiding stale closures and unnecessary re-renders from subscribing to all store fields.

2. **Infeasible results stored as simulation result** — When the solver returns `feasible: false`, we call `setSimulationResult` (not `setSimulationError`) so the preview component can display the hard conflicts visually. This matches the design doc's intent for the preview to show conflicts.

3. **Timeout treated as error** — When `timed_out: true`, we call `setSimulationError` with a localized message since there's no useful result to display.

4. **Settings panel state preserved on error** — On network errors, only `simulationError` is set. All override state (tasks, constraints, members, settings) remains intact so the admin can retry.

5. **Multiple runs supported** — The handler is stateless and can be called repeatedly within the same session. Each run clears the previous error before starting.

## How it connects

- Depends on: `sandboxStore` (task 4.1), `sandboxPayloadBuilder` (task 4.2), `SandboxSettingsPanel` (task 6.2), backend simulation endpoint (task 1.1)
- Used by: `SandboxSchedulePreview` (task 7.1) subscribes to `lastSimulationResult` and `simulationError` to display results
- Enables: Publish/discard flows (task 8.x) which operate on the simulation results

## How to run / verify

1. Enter the sandbox from a group with a draft version
2. Click "Run Simulation" — should show loading state
3. On success: preview updates with assignments
4. On timeout: error message "The solver timed out..." appears
5. On infeasibility: result stored with conflicts visible in preview
6. On network error: error message appears, settings panel state unchanged
7. Multiple runs: click again after modifying parameters — new results replace old

## What comes next

- Task 7.3: Home-leave preview in schedule preview
- Task 8.x: Publish and discard flows

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): wire up simulation run trigger with error handling"
```
