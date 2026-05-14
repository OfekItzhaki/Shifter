# 244 — BalanceSlider Component

## Phase

Home-Leave Slider — Frontend (Task 8.1)

## Purpose

Provides an intuitive horizontal slider control (0–100) that lets admins visually adjust the balance between "more people at base" and "more people home." Replaces the abstract `priority_weight` concept with a single, accessible input.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/home-leave/BalanceSlider.tsx` | Controlled range input component with Hebrew labels, gradient track, keyboard accessibility (arrows ±1, Page Up/Down ±10), ARIA attributes, and disabled state support |
| `apps/web/__tests__/home-leave/balanceSlider.test.tsx` | 10 unit tests covering rendering, value display, onChange callback, clamping, keyboard navigation, ARIA attributes, and disabled state |

## Key decisions

- **Native `<input type="range">`** — Uses the browser's native range input for built-in keyboard support (arrow keys ±1) and accessibility, with custom styling via Tailwind pseudo-element selectors.
- **Page Up/Down handled via `onKeyDown`** — The native range input doesn't support ±10 jumps, so we intercept `PageUp`/`PageDown` keys manually.
- **Gradient track overlay** — A blue→indigo→green gradient visually communicates the "base" (blue) to "home" (green) spectrum.
- **RTL-aware labels** — Left label = "יותר אנשים בבסיס" (base), right label = "יותר אנשים בבית" (home). In RTL context, the flex layout naturally handles direction.
- **Controlled component** — Accepts `value` and `onChange` props; no internal state. Parent manages the value.
- **`aria-live="polite"`** on the numeric display — Screen readers announce value changes without interrupting.

## How it connects

- Used by `HomeLeaveConfigPanel` (task 8.4) to replace the old priority_weight input
- The `onChange` callback feeds into `useHomeLeavePreview` hook (task 8.2) for debounced preview requests
- The `value` prop is sourced from the stored `balanceValue` in `HomeLeaveConfigDto`

## How to run / verify

```bash
cd apps/web
npx vitest --run __tests__/home-leave/balanceSlider.test.tsx
```

All 10 tests should pass.

## What comes next

- Task 8.2: `useHomeLeavePreview` custom hook (debounced preview API calls)
- Task 8.3: `ImpactSummary` component (displays preview results)
- Task 8.4: Integration into `HomeLeaveConfigPanel`

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-slider): add BalanceSlider component with tests"
```
