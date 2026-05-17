# Step 302 — Sandbox Publish Flow UI

## Phase

Phase 8 — Draft Simulation Sandbox (Publish & Discard Flows)

## Purpose

Implements the publish flow UI in the sandbox settings panel. When the admin clicks "Publish", the frontend constructs a `PublishSandboxRequest` from the current sandbox state (transforming Map-based overrides into the backend DTO format), calls `POST /publish-sandbox`, and on success exits the sandbox and navigates to the group schedule view. On failure (409 conflict or 500 error), an error message is displayed while preserving the sandbox state.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/simulation.ts` | Added `publishSandbox` API function and TypeScript interfaces for `PublishSandboxRequest`, `TaskOverrideDto`, `ConstraintOverrideDto`, `SettingsOverrideDto` |
| `apps/web/components/sandbox/SandboxSettingsPanel.tsx` | Added "Publish" button alongside the existing "Discard" button, with `handlePublish` callback that transforms sandbox state to backend DTO format and handles success/error cases |
| `apps/web/messages/en.json` | Added `sandbox.publish`, `sandbox.publishing`, `sandbox.publishSuccess`, `sandbox.publishConflict`, `sandbox.publishError` i18n keys |
| `apps/web/messages/he.json` | Added Hebrew translations for publish flow keys |
| `apps/web/messages/ru.json` | Added Russian translations for publish flow keys |

## Key decisions

- **DTO transformation on frontend**: The sandbox store uses `Map<string, TaskOverride>` and `Map<string, ConstraintOverride>` for efficient lookups, but the backend expects flat arrays of DTOs. The `handlePublish` function iterates over the Maps and constructs the correct DTO shape for each override action (add/edit/remove).
- **Severity detection for constraints**: Since the frontend stores constraints as either `HardConstraintDto` or `SoftConstraintDto`, we detect severity by checking for the presence of a `weight` field (soft constraints have weight, hard constraints don't).
- **Settings DTO is null when no overrides**: If no settings were modified, `settingsOverrides` is sent as `null` rather than an empty object, matching the backend's nullable `SettingsOverrideDto?` parameter.
- **Navigation after publish**: Uses `router.push(/groups/{groupId})` to navigate back to the group detail page, which will show the published schedule.
- **Error handling**: 409 Conflict shows a specific "version already published" message; all other errors show a generic failure message. In both cases, sandbox state is preserved so the admin can retry or adjust.

## How it connects

- Depends on: `sandboxStore` (task 4.1), backend `POST /publish-sandbox` endpoint (task 2.2), `discardVersion` API (existing)
- Used by: Admin users who want to persist their sandbox modifications and publish the draft version
- Complements: Discard flow (task 8.2) which was already implemented in the same panel

## How to run / verify

1. Enter the simulation sandbox from a draft schedule
2. Make some modifications (add/edit/remove tasks, constraints, members, settings)
3. Click "Publish" — should call the backend and navigate to the group page on success
4. To test error handling: attempt to publish an already-published version — should show the 409 conflict message

## What comes next

- Task 8.3: Navigation guards (beforeunload warning when navigating away without publish/discard)
- Task 9.1: Frontend access control (hide sandbox for non-admin users)

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): implement publish flow UI with DTO transformation and error handling"
```
