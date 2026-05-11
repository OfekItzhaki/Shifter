# 139 — Dark Mode Toggle

## Phase
Phase 8 — UX

## Purpose
Many soldiers use the app at night (guard duty, night shifts). Dark mode reduces eye strain and battery usage on OLED screens. Users can choose light, dark, or system-auto.

## What was built

### Files created:

| File | Description |
|------|-------------|
| `apps/web/lib/store/themeStore.ts` | Zustand store with localStorage persistence for theme preference (light/dark/system) |
| `apps/web/components/ThemeProvider.tsx` | Applies `dark` class to `<html>`, listens for system theme changes |
| `apps/web/components/DarkModeToggle.tsx` | Three-button toggle (☀️ 🌙 💻) for the sidebar |

### Files modified:

| File | Change |
|------|--------|
| `apps/web/tailwind.config.ts` | Added `darkMode: "class"` |
| `apps/web/app/providers.tsx` | Wrapped children with ThemeProvider |
| `apps/web/components/shell/AppShell.tsx` | Added DarkModeToggle to sidebar, dark background class on main content |
| `apps/web/app/globals.css` | Added dark mode CSS overrides for cards, text, inputs, topbar, scrollbars |

## Key decisions

1. **Class-based dark mode** — Using Tailwind's `darkMode: "class"` strategy so it works independently of the OS setting (user can override).

2. **Three options** — Light (☀️), Dark (🌙), System/Auto (💻). System follows the OS preference and updates in real-time.

3. **CSS overrides approach** — Rather than adding `dark:` classes to every component (hundreds of files), we use global CSS selectors that override common Tailwind classes when `.dark` is on `<html>`. This gives us dark mode across the entire app with minimal code changes.

4. **Sidebar stays dark** — The sidebar is already dark navy (#0f172a) in both modes. Only the main content area changes.

5. **Persisted in localStorage** — Theme preference survives page refreshes.

## How it connects
- ThemeProvider wraps the entire app via providers.tsx
- DarkModeToggle sits in the sidebar next to the LanguageSwitcher
- The `dark` class on `<html>` triggers all Tailwind `dark:` utilities and our CSS overrides

## How to run / verify
1. Open the app
2. In the sidebar, find the moon icon toggle (below language switcher)
3. Click 🌙 — the main content area goes dark
4. Click ☀️ — back to light
5. Click 💻 — follows your OS setting
6. Refresh — preference persists

## Git commit

```bash
git add -A && git commit -m "feat(phase8): dark mode toggle — light/dark/system with persisted preference"
```
