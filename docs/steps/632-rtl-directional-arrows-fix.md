# 632 — RTL Directional Arrows Fix

## Phase
UX Polish — RTL/i18n

## Purpose
Fix all directional arrows and chevrons in the Next.js frontend to respect RTL (Hebrew) text direction. Forward-pointing SVG chevrons need to flip in RTL mode, and hardcoded `←` back arrows need to render as `→` when the locale is Hebrew.

## What was built

### SVG chevrons — added `rtl:rotate-180`
| File | Location | Description |
|------|----------|-------------|
| `apps/web/app/groups/page.tsx` | Group card chevron | Forward indicator on group cards |
| `apps/web/app/spaces/page.tsx` | Space card chevron | Forward indicator on space selection cards |
| `apps/web/app/home/page.tsx` | "viewFullGuide" link | Chevron after "View full guide" button |
| `apps/web/app/home/page.tsx` | "viewAllUpdates" link | Chevron after "View all updates" link |
| `apps/web/components/onboarding/OnboardingPanel.tsx` | Step CTA button | Chevron in onboarding step action button |

### Text arrows — locale-aware rendering
| File | Change |
|------|--------|
| `apps/web/app/forgot-password/page.tsx` | Added `useLocale` import, `backArrow` variable, replaced `←` |
| `apps/web/app/reset-password/page.tsx` | Added `useLocale` import, `backArrow` variable, replaced `←` |
| `apps/web/app/verify-email/page.tsx` | Added `useLocale` import, `backArrow` variable, replaced `←` |
| `apps/web/app/pricing/page.tsx` | Added `useLocale` import, `backArrow` variable, replaced `←` |
| `apps/web/app/onboarding/page.tsx` | Added `backArrow` variable (already had `useLocale`), replaced 2× `←` |

## Key decisions
- Used Tailwind's `rtl:rotate-180` variant for SVG chevrons — zero JS overhead, pure CSS.
- Used `const backArrow = locale === "he" ? "→" : "←"` pattern for text arrows — simple, readable, and consistent across all files.
- Did NOT flip calendar/schedule navigation arrows (prev/next buttons) — those are semantically correct regardless of text direction.
- Did NOT touch `→` in `ScheduleDiffView.tsx` or admin person page — those are semantic "changed to" indicators, not directional.

## How it connects
- Builds on the existing `rtl:` Tailwind variant support already configured in the project.
- Complements the i18n infrastructure (`next-intl`, `useLocale`) already in place.
- Ensures visual consistency for Hebrew users across all navigation patterns.

## How to run / verify
1. Switch the app locale to Hebrew (`he`)
2. Verify group cards, space cards, home page links, and onboarding panel chevrons point left (←) in RTL
3. Verify "back" links show `→` arrow in Hebrew and `←` in English
4. Verify calendar prev/next buttons are NOT affected

## What comes next
- Any new forward-pointing chevrons added in the future should include `rtl:rotate-180`
- Any new back arrows should use the `backArrow` pattern

## Git commit

```bash
git add -A && git commit -m "fix(i18n): flip directional arrows and chevrons for RTL support"
```
