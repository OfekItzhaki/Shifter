# Step 629 — Mobile-Optimized Layout and RTL Verification for /pick

## Phase

Feature: Shift Picker Lite — Task 9.1

## Purpose

Verify that the `/pick` route satisfies all mobile-optimized layout and RTL requirements. This is a verification step — the implementation from prior tasks (8.1–8.3, 5.1, 6.1, 7.1) already meets all criteria.

## What was verified

| Sub-task | Status | Evidence |
|----------|--------|----------|
| No sidebar — standalone render | ✅ Already done | No `app/pick/layout.tsx` exists; page renders directly under root layout with its own `PickerHeader` |
| Single-column layout, no horizontal scroll < 640px | ✅ Already done | `<main className="flex-1 w-full max-w-lg mx-auto px-4 py-4 space-y-4">` in `page.tsx` |
| RTL inherited from root HTML | ✅ Already done | Root `layout.tsx` sets `dir={isRtl(locale) ? "rtl" : "ltr"}` on `<html>` element |
| Minimum 16px body text, 14px secondary labels | ✅ Already done | `text-base` (16px) for body text, `text-sm` (14px) for secondary labels across all pick components |
| 44x44px minimum tap targets | ✅ Already done | `min-h-[44px] min-w-[44px]` on all buttons in PickerHeader, PickerTabs, GroupSelector |
| Existing color scheme and design tokens | ✅ Already done | All components use slate/sky Tailwind classes consistent with the app |
| Skeleton placeholders while loading | ✅ Already done | `LoadingCard` shown during `phase === "loading"` and as `Suspense` fallback for lazy tabs |

## Files reviewed (no changes needed)

- `apps/web/app/layout.tsx` — root layout with `dir="rtl"` support
- `apps/web/app/pick/page.tsx` — pick route page with single-column layout
- `apps/web/components/pick/PickerHeader.tsx` — 44x44 tap targets, design tokens
- `apps/web/components/pick/PickerTabs.tsx` — 44x44 tap targets, text-sm labels
- `apps/web/components/pick/GroupSelector.tsx` — 44x44 tap targets, text-base body, text-sm secondary
- `apps/web/components/groups/selfService/LoadingCard.tsx` — skeleton placeholders

## Key decisions

- No code changes required — all requirements were already satisfied by the component implementations in tasks 5.1, 6.1, 7.1, 8.1–8.3.
- The `/pick` route intentionally has no dedicated `layout.tsx`, which means it inherits the root layout (with RTL and i18n) but avoids the sidebar shell used by other routes.

## How it connects

- Depends on: Tasks 5.1 (PickerHeader), 6.1 (GroupSelector), 7.1 (PickerTabs), 8.1–8.3 (PickPage wiring)
- Required by: Task 10 (checkpoint), Task 11 (property tests)

## How to verify

1. Open the app in a mobile viewport (< 640px width)
2. Navigate to `/pick`
3. Confirm: no sidebar, single-column layout, no horizontal scroll
4. Confirm: text is RTL, Hebrew labels display correctly
5. Confirm: all buttons are easily tappable (44x44px minimum)
6. Confirm: skeleton placeholders appear during loading
7. Confirm: color scheme matches the rest of the application

## What comes next

- Task 10: Checkpoint — full picker UI wired
- Task 11: Slot sorting and capacity formatting property tests

## Git commit

```bash
git add -A && git commit -m "docs(shift-picker-lite): verify mobile layout and RTL for /pick route"
```
