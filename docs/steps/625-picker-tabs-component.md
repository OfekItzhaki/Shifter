# 625 — PickerTabs Component

## Phase

Shift Picker Lite — UI Components (Task 7.1)

## Purpose

Provides a tab switcher component for the `/pick` route that allows members to toggle between the "Available Slots" and "My Shifts" views. This is a core navigation element of the shift picker experience.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/pick/PickerTabs.tsx` | Tab switcher component with two tabs: "משמרות פנויות" (slots) and "המשמרות שלי" (my-shifts) |
| `apps/web/__tests__/pick/pickerTabs.test.tsx` | Unit tests verifying rendering, accessibility, active state, callbacks, and tap target sizing |

## Key decisions

- **Follows existing tab pattern**: Uses the same `bg-slate-100` container with `bg-white` active indicator pattern found in the group detail page tabs.
- **44x44px minimum tap targets**: Enforced via `min-h-[44px] min-w-[44px]` Tailwind classes for mobile accessibility (Requirement 6.1).
- **ARIA roles**: Uses `role="tablist"` and `role="tab"` with `aria-selected` for screen reader accessibility.
- **i18n via next-intl**: All labels resolved from the `pick` namespace using `useTranslations("pick")`.
- **Exported type**: `PickerTab` type alias exported for reuse by the parent `PickPage` component.

## How it connects

- Used by `PickPage` (task 8.1/8.3) to switch between `SlotBrowserTab` and `MyShiftsTab`
- Depends on i18n keys added in task 4.1 (`pick.tabs.slots`, `pick.tabs.myShifts`)
- Follows the same visual language as the group detail page tab bar

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/pick/pickerTabs.test.tsx
```

All 8 tests should pass.

## What comes next

- Task 8.1: PickPage route component wires PickerTabs into the page state machine
- Task 8.3: PickerTabs controls which reused tab (SlotBrowserTab / MyShiftsTab) is rendered

## Git commit

```bash
git add -A && git commit -m "feat(pick): implement PickerTabs tab switcher component"
```
