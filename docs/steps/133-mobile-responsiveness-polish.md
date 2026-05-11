# 133 — Mobile Responsiveness Polish

## Phase
Phase 8 — UX & Mobile

## Purpose
The app is primarily used on mobile devices (soldiers checking their schedules on phones). This step ensures every page looks great and is fully usable on small screens with touch interactions.

## What was built

### Files modified:

| File | Change |
|------|--------|
| `apps/web/app/layout.tsx` | Added viewport meta tag, apple-mobile-web-app meta tags, theme-color |
| `apps/web/app/globals.css` | Added mobile touch improvements: prevent iOS zoom on inputs, momentum scrolling, safe area padding, scrollbar hiding, utility classes for mobile stacking |
| `apps/web/tailwind.config.ts` | Added `scrollbar-hide` utility plugin |
| `apps/web/components/shell/AppShell.tsx` | Responsive content padding (`clamp()`), improved mobile topbar with logo + notification bell, nav items close sidebar on tap |
| `apps/web/components/Modal.tsx` | Bottom-sheet style on mobile (slides up from bottom), drag handle indicator, centered on desktop |
| `apps/web/components/DraftScheduleModal.tsx` | Bottom-sheet on mobile, responsive padding, drag handle, wrapped footer buttons |
| `apps/web/components/schedule/ScheduleTaskTable.tsx` | Reduced cell padding on mobile, smaller font, negative margin for edge-to-edge tables |
| `apps/web/components/schedule/ScheduleTable2D.tsx` | Same mobile table improvements |
| `apps/web/app/groups/page.tsx` | Full-width create form on mobile, responsive headings, tighter spacing |
| `apps/web/app/groups/[groupId]/page.tsx` | Responsive header (smaller avatar, truncated name, icon-only admin button on mobile), scrollable tabs with smaller text, tighter padding |
| `apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx` | Stacking draft banner buttons, full-width search, touch-friendly day tabs with negative margin for edge-to-edge scroll |
| `apps/web/app/schedule/my-missions/page.tsx` | Full-width range selector, horizontal scroll day buttons, full-width search |
| `apps/web/app/profile/page.tsx` | Stacking hero section on mobile (avatar centered), single-column info cards grid |
| `apps/web/app/notifications/page.tsx` | Responsive heading sizes |

## Key decisions

1. **Bottom-sheet modals on mobile** — Modals slide up from the bottom on phones (like native iOS/Android sheets) with a drag handle. On desktop they remain centered dialogs.
2. **Edge-to-edge tables** — Schedule tables use negative margins on mobile to extend to screen edges, maximizing data visibility.
3. **Prevent iOS zoom** — Input font-size forced to 16px on mobile to prevent Safari's auto-zoom behavior.
4. **Icon-only buttons on mobile** — Admin mode toggle shows only the shield icon on small screens, full text on desktop.
5. **Horizontal scroll for tabs/days** — Instead of wrapping (which wastes vertical space), tabs and day selectors scroll horizontally with hidden scrollbars.
6. **Safe area support** — Content respects notched phone safe areas via `env(safe-area-inset-bottom)`.
7. **Sidebar closes on navigation** — Tapping a nav link on mobile automatically closes the sidebar overlay.

## How it connects
- Builds on the existing responsive sidebar system (step 007, 025)
- All existing functionality preserved — this is purely visual/UX improvement
- Prepares the app for the service worker offline caching (next step)

## How to run / verify
1. Open the app on a phone (or Chrome DevTools mobile emulator, iPhone 12/13/14 size)
2. Verify:
   - Sidebar opens/closes smoothly, closes on nav tap
   - Mobile topbar shows hamburger + logo + notification bell
   - My Missions page: range selector fills width, day buttons scroll horizontally
   - Group detail: tabs scroll horizontally, admin button is icon-only
   - Schedule tables extend edge-to-edge, readable on 375px width
   - Modals slide up from bottom with drag handle
   - Profile page: avatar + info stacks vertically, cards are single column
   - No horizontal overflow on any page
   - Inputs don't trigger zoom on iOS

## What comes next
- Offline schedule caching (service worker)
- Better notification preferences UI
- Landing page / marketing page

## Git commit

```bash
git add -A && git commit -m "feat(phase8): mobile responsiveness polish — bottom-sheet modals, edge-to-edge tables, touch-friendly UI"
```
