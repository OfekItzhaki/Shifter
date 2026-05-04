# Step 100 — JSX Parse Fix & Favicon Update

## Phase
Phase 9 — Polish & Hardening

## Purpose
Two small but blocking issues: a JSX parse error that crashed the schedule page, and the favicon still pointing to the old SVG instead of the new `favicon.jpeg`.

## What was built

### `apps/web/components/schedule/ScheduleTaskTable.tsx`

The `return` statement had two sibling top-level elements — a `<div>` and the `{cantMakeIt && ...}` modal — without a wrapping fragment. JSX requires a single root element. Wrapped both in a `<>...</>` fragment.

Before:
```tsx
return (
  <div className="space-y-6">
    ...
  </div>

  {/* Can't make it modal */}
  {cantMakeIt && ...}
);
```

After:
```tsx
return (
  <>
    <div className="space-y-6">
      ...
    </div>

    {/* Can't make it modal */}
    {cantMakeIt && ...}
  </>
);
```

### `apps/web/app/layout.tsx`

The `metadata.icons` was still pointing to `/favicon.svg`. Updated all three icon entries (`icon`, `shortcut`, `apple`) to `/favicon.jpeg` which is the new branding asset already present in `public/`.

## Key decisions

- Fragment wrapper is the minimal fix — no structural changes to the component.
- All three icon slots updated together so the favicon is consistent across browsers and iOS home screen.

## How to run / verify

1. Open the app — the parse error on the schedule/today page should be gone.
2. Check the browser tab — it should show the new favicon.jpeg icon.
3. On iOS, add to home screen — the apple-touch-icon should also use the new image.

## What comes next

- LTS v1.5 tag.

## Git commit

```bash
git add -A && git commit -m "fix(schedule): wrap ScheduleTaskTable return in fragment; fix favicon to use favicon.jpeg"
```
