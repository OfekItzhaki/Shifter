# 215 — Template Picker Qualifications/Reasons & Unavailability Reasons Panel

## Phase

Feature: Qualification Templates & Unavailability Reasons (Frontend)

## Purpose

Extends the `GroupTemplatePicker` to create qualifications and seed unavailability reasons when applying a template, and adds a settings panel for managing unavailability reasons per space.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/GroupTemplatePicker.tsx` | Added qualification creation loop (skipping 409 conflicts) and unavailability reason seeding after existing task/constraint creation |
| `apps/web/lib/api/unavailabilityReasons.ts` | Fixed `seedReasons` request body to match backend's `reasonDisplayNames` field |
| `apps/web/components/UnavailabilityReasonsPanel.tsx` | New settings panel for managing unavailability reasons — list, add, inline edit, deactivate, with count indicator (X/50) |

## Key decisions

- **409 handling in qualification loop**: Each qualification is created individually; if the backend returns 409 (duplicate name), we silently skip and continue. Any other error aborts the entire template application.
- **Seed reasons call is fire-and-forget**: The backend handles the "space already has reasons" check, so the frontend just sends the request without pre-checking.
- **Fixed API client mismatch**: The `seedReasons` function was sending `{ reasons }` but the backend expects `{ reasonDisplayNames }`. Fixed to match the `SeedUnavailabilityReasonsRequest` record.
- **Panel follows HomeLeaveConfigPanel pattern**: Same card styling, loading state, and error handling approach.
- **Hebrew labels**: Title "סיבות אי-זמינות", add button "הוסף סיבה", save button "שמור".

## How it connects

- `GroupTemplatePicker` now uses `createGroupQualification` from `@/lib/api/groups` and `seedReasons` from `@/lib/api/unavailabilityReasons`
- `UnavailabilityReasonsPanel` uses the full CRUD API from `@/lib/api/unavailabilityReasons`
- The panel can be embedded in space settings pages (similar to how `HomeLeaveConfigPanel` is used in group settings)

## How to run / verify

1. Apply a template in the group creation flow → qualifications should appear in the group's qualification list
2. Apply a template to a space with no reasons → reasons should be seeded
3. Apply a template to a space that already has reasons → no new reasons created (backend handles)
4. Open the unavailability reasons settings panel → should show list, allow add/edit/delete

## What comes next

- Task 7.1: Update unavailability form to show reason picker
- Task 7.2: Wire reason selection to presence window creation
- Task 7.3: Display reason in presence window views

## Git commit

```bash
git add -A && git commit -m "feat(qualification-templates): template picker creates qualifications/reasons + settings panel"
```
