# 296 — Sandbox Members Tab

## Phase

Phase — Draft Simulation Sandbox (Frontend UI)

## Purpose

Implements the Members tab in the sandbox settings panel, allowing admins to include/exclude members from the simulation. This fulfills Requirements 4.1–4.4 of the draft-simulation-sandbox spec.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/sandbox/SandboxMembersTab.tsx` | New component: full member list with toggle controls, search, active/total count, role/qualification badges, and visual dimming for excluded members |
| `apps/web/components/sandbox/SandboxSettingsPanel.tsx` | Updated to import and render `SandboxMembersTab` instead of the placeholder `MembersTabContent` |
| `apps/web/messages/en.json` | Added `sandbox.members.*` i18n keys (searchPlaceholder, noMembers, noResults, excluded, included) |
| `apps/web/messages/he.json` | Added Hebrew translations for `sandbox.members.*` keys |
| `apps/web/messages/ru.json` | Added Russian translations for `sandbox.members.*` keys |

## Key decisions

- **Checkbox toggle pattern** — Matches existing project patterns (OverrideModal, SmartImportModal) using `<input type="checkbox">` with `accent-blue-500`
- **Visual dimming + line-through** — Excluded members get `opacity-50` and `line-through` text decoration for clear visual distinction
- **Search filter** — Only shown when there are more than 5 members to avoid clutter on small groups
- **PersonId as display name** — Per task instructions, showing `personId` for now (can be replaced with display names when available)
- **Role/qualification badges** — Indigo for roles, emerald for qualifications — consistent color coding for quick scanning
- **Included/Excluded status badge** — Small colored badge on each row for accessibility (not relying solely on opacity)

## How it connects

- Reads `baseline.people` from the Zustand sandbox store for the member list
- Reads `memberExclusions` Set from the store to determine toggle state
- Calls `store.toggleMember(personId)` on checkbox change
- The payload builder (`sandboxPayloadBuilder.ts`) already filters excluded members from the `People` list when constructing the override payload

## How to run / verify

1. Enter the simulation sandbox from a group with a draft version
2. Click the "Members" tab in the settings panel
3. Verify all members from the baseline are listed with checkboxes
4. Toggle a member off — they should appear dimmed with line-through and "Excluded" badge
5. Toggle them back on — they should restore to full opacity with "Included" badge
6. Verify the active/total count updates correctly
7. If more than 5 members, verify the search input appears and filters correctly

## What comes next

- Task 6.6: Implement Settings tab in settings panel
- Task 7.1/7.2: Wire up simulation execution to verify excluded members are actually removed from the solver payload

## Git commit

```bash
git add -A && git commit -m "feat(sandbox): implement members tab with toggle controls"
```
