# Step 298 — Sandbox Constraints Tab

## Phase

Phase 6 — Draft Simulation Sandbox (Frontend UI)

## Purpose

Implements the Constraints tab in the sandbox settings panel, allowing admins to view, add, edit, and remove scheduling constraints within the simulation sandbox. This enables "what-if" experimentation with constraint configurations before committing changes.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/sandbox/SandboxConstraintsTab.tsx` | New component implementing the full constraints tab with list display, add/edit/remove controls, visual status indicators, and inline form |
| `apps/web/components/sandbox/SandboxSettingsPanel.tsx` | Updated to import and use `SandboxConstraintsTab` instead of the placeholder `ConstraintsTabContent` |
| `apps/web/messages/en.json` | Added `sandbox.constraints` i18n keys (English) |
| `apps/web/messages/he.json` | Added `sandbox.constraints` i18n keys (Hebrew) |
| `apps/web/messages/ru.json` | Added `sandbox.constraints` i18n keys (Russian) |

## Key decisions

1. **Unified constraint list** — Merges both hard and soft constraints from the baseline with override state into a single list, each tagged with a `status` field for visual differentiation.
2. **Visual indicators** — Green ring/badge for added, amber for modified, red with line-through for removed constraints. Matches the pattern established in the Tasks tab.
3. **Reuses `ConstraintPayloadEditor`** — The existing payload editor component provides rule-type-aware form fields, keeping the sandbox form consistent with the main constraint management UI.
4. **Undo for removed constraints** — Instead of permanently removing, removed baseline constraints show an "Undo" button that restores them by deleting the override entry from the store.
5. **`crypto.randomUUID()`** — Used for generating new constraint IDs, matching the project's existing pattern (no external uuid package dependency).

## How it connects

- Reads from and writes to the `useSandboxStore` Zustand store (task 4.1)
- Uses `addConstraint`, `editConstraint`, `removeConstraint` store actions
- Integrates with `SandboxSettingsPanel` as the constraints tab content
- The `buildOverridePayload` function (task 4.2) consumes the constraint overrides to construct the simulation payload
- Reuses the existing `ConstraintPayloadEditor` component for rule-type-aware payload editing

## How to run / verify

1. Enter the simulation sandbox from a draft schedule
2. Navigate to the "Constraints" tab in the settings panel
3. Verify baseline constraints are displayed with "unmodified" styling
4. Click "Add Constraint" — fill in rule type, severity, scope, and payload
5. Verify added constraints show green indicator
6. Edit an existing constraint — verify amber indicator appears
7. Remove a constraint — verify red/strikethrough styling and "Undo" button
8. Run `npx tsc --noEmit` from `apps/web` to confirm no type errors

## What comes next

- Task 6.5: Members tab implementation
- Task 6.6: Settings tab implementation
- Task 7.1–7.2: Schedule preview and simulation execution

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): implement constraints tab in settings panel"
```
