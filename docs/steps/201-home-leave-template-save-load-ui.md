# Step 201 — Home-Leave Template Save/Load UI

## Phase

Phase 6 — Frontend UI (Home-Leave Scheduling)

## Purpose

Allows group admins to save the current home-leave configuration as a named template and load previously saved templates to quickly populate the form. This avoids repetitive manual configuration when setting up multiple closed-base groups with similar leave parameters.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/home-leave/HomeLeaveConfigPanel.tsx` | Extended with `TemplateSaveLoadSection` sub-component and `HomeLeaveTemplateDto` interface |

### Changes to `HomeLeaveConfigPanel.tsx`

- Added `HomeLeaveTemplateDto` export interface for template data
- Added `TemplateSaveLoadSection` internal component with:
  - "טען תבנית" (Load Template) dropdown — fetches templates via `GET /spaces/{spaceId}/home-leave-templates`, sorted by `created_at` desc (server-side)
  - "שמור כתבנית" (Save as Template) button — opens inline form for template name, calls `POST /spaces/{spaceId}/home-leave-templates`
  - On template select: populates form fields without auto-saving (user must click Save separately)
  - Error handling: 409 → "שם התבנית כבר קיים", 400 → "שם התבנית לא תקין"
- Integrated `TemplateSaveLoadSection` into the main panel, passing current form values and an `onLoadTemplate` callback that updates form state

## Key decisions

- **Template UI lives inside `HomeLeaveConfigPanel`** rather than as a separate component — it needs direct access to the form values for both saving (reads current values) and loading (writes to form state). Keeping it co-located avoids prop drilling.
- **Inline save form** instead of a modal — keeps the UX lightweight and consistent with the rest of the settings panel.
- **Dropdown resets to placeholder after selection** — uses controlled `value=""` so the same template can be re-selected.
- **No auto-save on template load** — per requirements, the user must explicitly click "שמור" to persist the loaded values.

## How it connects

- Uses the existing `POST /spaces/{spaceId}/home-leave-templates` and `GET /spaces/{spaceId}/home-leave-templates` API endpoints (implemented in task 4.2)
- The `HomeLeaveConfigPanel` is rendered inside `SettingsTab` when `isClosedBase` is true
- Template values match the same fields as the config form (minRestHours, eligibilityThresholdHours, leaveCapacity, leaveDurationHours)

## How to run / verify

1. Enable "בסיס סגור" toggle in group settings
2. The "הגדרות חופשות" panel should appear with a "תבניות" section at the bottom
3. Fill in config values and click "שמור כתבנית" — enter a name and save
4. The template should appear in the "טען תבנית" dropdown
5. Change form values, then select the saved template — form should populate with template values without saving
6. Try saving a template with a duplicate name — should show "שם התבנית כבר קיים"

## What comes next

- Task 13.2: Render home-leave slots on schedule timeline
- Task 14: Checkpoint — verify frontend components render correctly

## Git commit

```bash
git add -A && git commit -m "feat(phase6): home-leave template save/load UI in config panel"
```
