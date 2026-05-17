# 295 — Sandbox Settings Panel

## Phase
Feature: Draft Simulation Sandbox

## Purpose
Creates the `SandboxSettingsPanel` component — the left panel in the sandbox split view. This component provides a tabbed interface for admins to modify scheduling parameters (tasks, constraints, members, settings) during a simulation session. It subscribes only to override state from the Zustand store, preventing re-renders when simulation results arrive.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/sandbox/SandboxSettingsPanel.tsx` | Main settings panel with 4 tabs (Tasks, Constraints, Members, Settings), a "Run Simulation" button, and placeholder tab content |
| `apps/web/messages/en.json` | Added `sandbox` translation namespace |
| `apps/web/messages/he.json` | Added `sandbox` translation namespace (Hebrew) |
| `apps/web/messages/ru.json` | Added `sandbox` translation namespace (Russian) |

## Key decisions

1. **Selective Zustand subscriptions** — The component subscribes to `taskOverrides`, `constraintOverrides`, `memberExclusions`, `settingsOverrides`, `baseline`, and `isSimulating` individually. It does NOT subscribe to `lastSimulationResult`, ensuring the settings panel never re-renders when simulation completes (Req 8.1, 8.4).

2. **Tab pattern** — Uses the same pill-style tab buttons found in `DraftScheduleModal` (bg-slate-100 container with rounded-lg active state), keeping visual consistency.

3. **Placeholder tab content** — Each tab renders summary counts and a placeholder message. Full implementations will come in tasks 6.3–6.6.

4. **Run Simulation button** — Included with an `onClick` placeholder (empty callback). Will be wired to the simulation API in task 7.2.

5. **i18n** — All user-facing strings use `next-intl` with the `sandbox` namespace, with translations in all 3 supported languages.

## How it connects

- **Reads from**: `useSandboxStore` (Zustand store created in task 4.1)
- **Used by**: `SimulationSandboxPage` (task 6.1 / future page component)
- **Tab content**: Tasks (6.3), Constraints (6.4), Members (6.5), Settings (6.6)
- **Run Simulation wiring**: Task 7.2

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # Type-check passes
```

The component can be visually verified once the sandbox page is assembled (task 6.1 provides the entry point).

## What comes next

- Tasks 6.3–6.6: Implement full tab content (task list, constraint list, member toggles, settings form)
- Task 7.2: Wire the "Run Simulation" button to the simulation API

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): create SandboxSettingsPanel component with tabs and i18n"
```
