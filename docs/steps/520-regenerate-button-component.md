# 520 — RegenerateButton Component

## Phase

Schedule Regeneration — Frontend (Task 10.1)

## Purpose

Provides the "Regenerate Schedule" action button in the admin schedule management panel. This is the entry point for the regeneration workflow — when clicked, it opens a confirmation dialog that triggers the backend regeneration pipeline.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/schedule/RegenerateButton.tsx` | Standalone button component with permission gating, published-version gating, and in-progress state handling |
| `apps/web/messages/en.json` | Added `admin.regeneration` i18n keys (English) |
| `apps/web/messages/he.json` | Added `admin.regeneration` i18n keys (Hebrew) |
| `apps/web/messages/ru.json` | Added `admin.regeneration` i18n keys (Russian) |
| `apps/web/__tests__/schedule/regenerateButton.test.tsx` | Unit tests covering all visibility/disabled/click requirements |

## Key decisions

- **Props-based permission gating**: Follows the same pattern as `SpaceBillingCard` — the parent component passes `hasPermission` and `hasPublishedVersion` as props rather than the button fetching permissions itself. This keeps the component pure and testable.
- **Violet color scheme**: Differentiates the regeneration action from the standard solver (sky/blue) and emergency solver (red) buttons already in the panel.
- **aria-busy attribute**: Used for accessibility when regeneration is in progress, allowing screen readers to announce the loading state.
- **Standalone component**: Designed to be dropped into the admin schedule page header alongside existing action buttons without modifying the page's internal state management.

## How it connects

- **Upstream**: The parent page (admin schedule management) provides the props by checking the user's permissions and whether a published version exists for the selected group.
- **Downstream**: The `onRegenerate` callback opens the `RegenerateConfirmDialog` (Task 10.2), which calls the `triggerRegeneration` API function (Task 10.4, already implemented).
- **i18n**: Translation keys are namespaced under `admin.regeneration` and shared with the confirm dialog component.

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/schedule/regenerateButton.test.tsx --reporter=verbose
```

All 11 tests should pass, covering:
- Permission gating (hidden when no permission)
- Published version gating (hidden when no published version)
- Disabled state with spinner when regeneration in progress
- Click callback fires correctly
- Click does not fire when disabled

## What comes next

- Task 10.2: `RegenerateConfirmDialog` — the modal that opens on button click
- Task 10.3: `RegenerationStatusIndicator` — shows real-time progress after triggering

## Git commit

```bash
git add -A && git commit -m "feat(schedule-regeneration): add RegenerateButton component with i18n and tests"
```
