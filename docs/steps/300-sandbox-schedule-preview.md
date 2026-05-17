# 300 — Sandbox Schedule Preview Component

## Phase

Phase — Draft Simulation Sandbox (Frontend)

## Purpose

Implements the right panel of the sandbox split view that displays simulation results. This component subscribes only to simulation-related state (`lastSimulationResult`, `isSimulating`, `simulationError`) to maintain reactive UI boundaries — the settings panel (left) and preview (right) re-render independently.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/sandbox/SandboxSchedulePreview.tsx` | Preview component that renders loading state, error messages (timeout/infeasibility), assignment table, and home-leave preview |
| `apps/web/messages/en.json` | Added `sandbox.preview.*` i18n keys (English) |
| `apps/web/messages/he.json` | Added `sandbox.preview.*` i18n keys (Hebrew) |
| `apps/web/messages/ru.json` | Added `sandbox.preview.*` i18n keys (Russian) |

## Key decisions

1. **Selective store subscriptions**: The component subscribes only to `lastSimulationResult`, `isSimulating`, `simulationError`, and `baseline` — never to override state. This prevents re-renders when the admin modifies tasks/constraints/members/settings in the left panel.

2. **Reuses `ScheduleTaskTable`**: The existing schedule table component is reused for rendering assignments. Solver output is mapped to `TaskAssignment[]` by cross-referencing baseline task slots for time windows and task type names.

3. **Home-leave section conditionally rendered**: The home-leave preview table only appears when `baseline.homeLeaveConfig.enabled` is true AND the solver returned home-leave assignments.

4. **Error states are localized**: Timeout and infeasibility messages use the project's `next-intl` i18n system with keys under `sandbox.preview.*`. Hard conflicts are listed individually when the schedule is infeasible.

## How it connects

- **Consumed by**: The sandbox page/modal that renders the split view (settings panel left, preview right)
- **Depends on**: `useSandboxStore` (Zustand), `ScheduleTaskTable` component, `next-intl` translations
- **Fed by**: Task 7.2 (simulation run trigger) which calls `setSimulationResult` / `setSimulationError` on the store

## How to run / verify

1. The component compiles without TypeScript errors
2. All i18n JSON files are valid
3. Visual verification: enter sandbox → run simulation → preview shows assignments or error states

## What comes next

- Task 7.2: Wire up the simulation run trigger (calls the API and updates store)
- Task 7.3: Implement home-leave preview enrichment with person names

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): add SandboxSchedulePreview component with i18n"
```
