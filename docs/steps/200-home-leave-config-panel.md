# Step 200 — Home-Leave Config Panel (Frontend)

## Phase

Phase 6 — Frontend UI (Home-Leave Scheduling)

## Purpose

Implements the "הגדרות חופשות" (Leave Settings) panel in the group settings page. This panel allows group admins to configure home-leave parameters (min rest hours, eligibility threshold, leave capacity, leave duration) for closed-base groups. The panel is conditionally rendered only when `isClosedBase = true`.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/home-leave/HomeLeaveConfigPanel.tsx` | New component — form panel with 4 numeric fields (min rest hours, eligibility threshold, leave capacity, leave duration), API integration (GET to populate, PUT to save), inline validation errors on 400, permission error toast on 403, success indicator on save |
| `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` | Modified — imports and renders `HomeLeaveConfigPanel` after the "בסיס סגור" toggle section |

## Key decisions

- **Self-contained component**: The panel manages its own state (loading, saving, errors) and fetches config independently via the API client. This keeps the SettingsTab clean and avoids prop-drilling.
- **Defaults on no saved config**: When the GET endpoint returns no saved config, the form populates with defaults (8, 24, 1, 48) matching requirement 2.9.
- **Inline validation errors**: On 400 responses, errors are parsed from the FluentValidation format and displayed next to the relevant field.
- **Permission error as inline toast**: On 403, a red banner shows "אין הרשאה לשנות הגדרות" rather than redirecting (since the global interceptor handles 403 redirects for GET requests, but PUT 403s should show inline feedback).
- **Conditional rendering**: The component returns `null` when `isClosedBase` is false, satisfying requirement 1.7.

## How it connects

- Depends on the `HomeLeaveConfigController` API endpoints (task 3.3) for GET/PUT operations
- Rendered inside `SettingsTab` which already receives `isClosedBase` from the group page state
- Uses the existing `apiClient` (axios) with auth interceptors for API calls
- Follows the same Section-style visual pattern as other SettingsTab sections

## How to run / verify

1. Start the frontend dev server: `cd apps/web && npm run dev`
2. Navigate to a group's settings tab
3. Toggle "בסיס סגור" ON → the "הגדרות חופשות" panel should appear
4. Toggle "בסיס סגור" OFF → the panel should disappear
5. With the panel visible, verify default values (8, 24, 1, 48) are shown
6. Change values and click "שמור" → should show "שמור ✓" on success
7. Submit invalid values → should show inline error messages

## What comes next

- Task 12.3: Template save/load UI within the leave settings panel
- Task 13.2: Home-leave slots on schedule timeline

## Git commit

```bash
git add -A && git commit -m "feat(home-leave): add leave settings config panel to group settings"
```
