# 624 — PickerHeader Component

## Phase

Shift Picker Lite — UI Components

## Purpose

Implements the minimal mobile header for the `/pick` route. The header provides group name display, back-navigation to the group selector, and a refresh button with loading spinner — all without any sidebar or desktop shell elements.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/pick/PickerHeader.tsx` | New component: minimal mobile header with back button, group name, and refresh button |

## Key decisions

- **44×44px minimum tap targets**: Both back and refresh buttons use `min-w-[44px] min-h-[44px]` to meet mobile accessibility requirements (Requirement 6.2)
- **No sidebar/shell elements**: The header is standalone — no sidebar navigation, no desktop shell (Requirement 6.3)
- **i18n from `pick` namespace**: All labels (`back`, `refresh`, `title`) resolved from the `pick` i18n namespace (Requirement 7.2)
- **RTL-aware arrow**: The back button uses a right-pointing arrow since the app is RTL
- **Spinner pattern**: Reuses the same SVG spinner pattern from `MutationButton` for visual consistency
- **Dark mode support**: Uses Tailwind dark mode classes for background, text, and border colors
- **Sticky header**: Uses `sticky top-0 z-20` so it stays visible during scroll

## How it connects

- Used by `PickPage` (task 8.1) as the top-level header in the slot-browser phase
- Calls `onBack` to transition the page state machine back to `group-select`
- Calls `onRefresh` to trigger data reload in the active tab
- Relies on `pick` i18n keys added in task 4.1

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

The component has no runtime dependencies beyond next-intl and Tailwind — it renders correctly when mounted with valid props.

## What comes next

- Task 6.1: GroupSelector component
- Task 7.1: PickerTabs component
- Task 8.1: PickPage route wiring (uses PickerHeader)

## Git commit

```bash
git add -A && git commit -m "feat(shift-picker-lite): implement PickerHeader component"
```
