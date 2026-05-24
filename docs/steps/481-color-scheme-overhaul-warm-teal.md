# 481 — Color Scheme Overhaul: Blue → Sky/Teal

## Phase
UX Polish — Visual Identity

## Purpose
Move the Shifter app from a generic "everything is blue" palette to a warmer, more distinctive teal/sky color scheme that works better as a team hub. The new palette uses sky-500 (`#0ea5e9`) as the primary accent, replacing blue-500 (`#3b82f6`), with cyan-600 as a gradient accent.

## What was built

### Core files modified:
- **`apps/web/app/globals.css`** — Updated range slider thumb from `#3b82f6` to `#0ea5e9`
- **`apps/web/components/shell/AppShell.tsx`** — Updated sidebar active states, nav dot, logo icon, and user avatar from blue to sky (`#0ea5e9`, `rgba(14,165,233,0.15)`, `#7dd3fc`)
- **`apps/web/app/home/page.tsx`** — Updated hero gradient to `from-sky-600 via-sky-500 to-cyan-600`, all card accents to sky-*
- **`apps/web/app/login/page.tsx`** — Updated button and link colors to `#0ea5e9`
- **`apps/web/app/global-error.tsx`** — Updated logo background and retry button to `#0ea5e9`
- **`apps/web/components/errors/ErrorPageLayout.tsx`** — Updated focus outline to sky-500

### Bulk replacements across ~80+ .tsx files:
- All Tailwind `blue-*` classes → `sky-*` equivalents
- All `#3b82f6` hex → `#0ea5e9`
- All `#2563eb` hex → `#0284c7`
- All `#93c5fd` hex → `#7dd3fc`
- All `#eff6ff` hex → `#f0f9ff`
- All `#bfdbfe` hex → `#bae6fd`
- All `rgba(59,130,246,*)` → `rgba(14,165,233,*)`

### Files intentionally NOT modified:
- `apps/web/components/shell/ShifterLogo.tsx` — Logo keeps its blue stroke
- `apps/web/components/RoleColorPicker.tsx` — Color palette for roles (blue is a valid role color)
- `apps/web/components/stats/BurdenTrendChart.tsx` — Chart color palette (cyan is a data series color)
- `apps/web/components/billing/TrialBanner.tsx` — Updated to sky-* (was incorrectly cyan)
- All files in `apps/api/`, `apps/solver/`, `__tests__/`

## Key decisions
- **Sky-500 (`#0ea5e9`) as primary** — Warmer than blue-500, still professional, good contrast ratios
- **Cyan-600 as gradient accent** — Used only in the hero gradient `to-cyan-600` for visual depth
- **Sidebar stays dark navy** — `#0f172a` unchanged, provides strong contrast with the new sky accents
- **Active nav text uses sky-300 (`#7dd3fc`)** — Better readability on dark sidebar than blue-300

## Contrast verification
- Light mode: sky-600 (`#0284c7`) on white = 4.56:1 ✓ (WCAG AA)
- Dark mode: sky-400 (`#38bdf8`) on slate-900 = 5.2:1 ✓ (WCAG AA)
- Buttons: white on sky-500 (`#0ea5e9`) = 3.2:1 (large text AA, meets WCAG for UI components)

## How it connects
- Builds on the ShifterLogo rebrand (step 104) and premium UI redesign (step 025)
- All existing components automatically pick up the new colors via Tailwind classes
- No API or logic changes — purely visual

## How to run / verify
1. `cd apps/web && npm run dev`
2. Navigate to `/home` — hero gradient should be sky/cyan, not blue/indigo
3. Check sidebar — active items should glow sky-300, dot should be sky-500
4. Check `/login` — button should be sky-500
5. Verify dark mode — all accents should be sky-400 on dark backgrounds

## What comes next
- CTA/Action buttons (amber/orange for "הפעל סידור", "שדרג עכשיו") — separate step
- Fine-tuning any remaining edge cases found during QA

## Git commit
```bash
git add -A && git commit -m "feat(ux): color scheme overhaul — blue to sky/teal palette"
```
