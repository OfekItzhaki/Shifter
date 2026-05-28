# 616 — Shift Templates Tab Component

## Phase

Self-Service Scheduling UI — Admin Tab Components

## Purpose

Implements the `ShiftTemplatesTab` admin component that allows group admins to manage recurring shift templates (CRUD operations) through a visual interface, eliminating the need to interact with the API directly.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/groups/selfService/ShiftTemplatesTab.tsx` | Full CRUD component for shift templates with create form, edit modal, delete confirmation, loading/error states |

## Key decisions

- **Modal editing pattern**: Edit uses a modal (via the existing `Modal` component) rather than inline editing, consistent with the MyShiftsTab cancel dialog pattern
- **Inline create form**: The create form appears inline below the header (toggled by the create button) for quick template creation without a modal
- **Client-side validation**: Uses `validateTemplateTimeRange` before API calls to provide instant feedback on invalid time ranges
- **Hebrew day names via i18n**: Uses `selfService.templates.days.*` i18n keys for day names in the dropdown and list display
- **Filter deleted templates**: Templates with `isDeleted: true` are filtered out client-side after fetch
- **Headcount clamping**: The number input clamps values between 1-999 on change

## How it connects

- Consumes API functions from `lib/api/selfService.ts` (listShiftTemplates, createShiftTemplate, updateShiftTemplate, deleteShiftTemplate)
- Uses validation from `lib/utils/selfServiceValidation.ts` (validateTemplateTimeRange)
- Uses error mapping from `lib/utils/selfServiceErrors.ts` (getSelfServiceErrorMessage)
- Uses formatting from `lib/utils/selfServiceFormat.ts` (formatTime24h)
- Uses i18n keys from `selfService.templates.*` namespace (already in he.json/en.json)
- Receives `tasks: GroupTaskDto[]` prop for the task dropdown
- Will be wired into the group detail page tab navigation in task 15.1

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # Type-check passes
```

The component will be visually testable once wired into the group detail page tab navigation (task 15.1).

## What comes next

- Task 11.1: SelfServiceConfigTab component (admin)
- Task 12.1: AdminOverridesTab component (admin)
- Task 15.1: Mode-conditional tab navigation wiring

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): implement ShiftTemplatesTab admin component"
```
