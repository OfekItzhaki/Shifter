# 299 — Sandbox Tasks Tab

## Phase

Phase 6 — Draft Simulation Sandbox (Frontend Settings Panel)

## Purpose

Implements the Tasks tab in the sandbox settings panel, allowing admins to add, edit, and remove task slots in the simulation sandbox. Tasks are visually distinguished by their override status (added, modified, removed) using color coding and badges.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/sandbox/SandboxTasksTab.tsx` | New component implementing the full Tasks tab with task list display, add/edit form, and remove/restore controls |
| `apps/web/components/sandbox/SandboxSettingsPanel.tsx` | Updated to import and render `SandboxTasksTab` instead of the placeholder |
| `apps/web/lib/store/sandboxStore.ts` | Added `restoreTask` action to cleanly undo any task override |
| `apps/web/messages/en.json` | Added English translations for task tab UI strings |
| `apps/web/messages/he.json` | Added Hebrew translations for task tab UI strings |
| `apps/web/messages/ru.json` | Added Russian translations for task tab UI strings |

## Key decisions

1. **Separate component file** — The Tasks tab is extracted into its own file (`SandboxTasksTab.tsx`) to keep the settings panel lean and allow independent development of each tab.

2. **Color-coded override indicators** — Tasks use left border colors (green=added, amber=modified, red=removed) plus small status badges for clear visual distinction.

3. **Tag-based role/qualification input** — Required roles and qualifications use a tag input pattern (type + Enter/click to add, × to remove) rather than a multi-select dropdown, since the values are free-form IDs.

4. **`restoreTask` store action** — Added a dedicated action that simply deletes the override entry from the map, cleanly restoring the task to its baseline state without creating a new "edit" override.

5. **Removed tasks stay visible** — Removed tasks remain in the list with strikethrough text and reduced opacity, with a restore button, so admins can undo removals.

## How it connects

- Reads from and writes to the `useSandboxStore` Zustand store (task overrides)
- The `buildOverridePayload` function (task 4.2) consumes these overrides to construct the simulation payload
- The settings panel (task 6.2) renders this tab when the "Tasks" tab is active
- The simulation run (task 7.2) will use the overrides set here

## How to run / verify

1. Enter the simulation sandbox from a draft schedule
2. The Tasks tab should display all baseline tasks
3. Click "Add Task" to open the form — fill in name, time window, headcount, burden level
4. Added tasks appear with a green left border and "Added" badge
5. Click the edit icon on any task to modify it — modified tasks show amber indicators
6. Click the trash icon to remove a task — removed tasks show red/strikethrough with a restore button
7. Click restore to undo a removal

## What comes next

- Task 6.4: Constraints tab implementation
- Task 7.2: Wiring the "Run Simulation" button to use overrides from all tabs

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): implement tasks tab in settings panel with add/edit/remove controls"
```
