# 209 — UX/Naming Fixes Batch

## Phase
Post-launch polish

## Purpose
Seven small UX and naming fixes from the backlog (items 2, 3, 5, 6, 10, 12, 22).

## What was built

### Fix 1: Starter instructions in "My Groups" tab (Item 2)
- **`apps/web/app/groups/page.tsx`** — When the user has no groups, the empty state now shows inline onboarding steps (create group, add members, define tasks, etc.) instead of just a plain "no groups" message.

### Fix 2: Email branding "Jobular" → "Shifter" (Item 3)
- **`apps/api/Jobuler.Infrastructure/Notifications/EmailInvitationSender.cs`** — Changed "Jobuler" to "Shifter" in the invitation email subject and body.

### Fix 3: Rename "Roles" tab → "הרשאות" (Item 5)
- **`apps/web/messages/he.json`** — Changed `groups.tabs.roles` from "תפקידים" to "הרשאות".

### Fix 4: Change "כישורים" → "הכשרות" (Item 6)
- **`apps/web/messages/he.json`** — Updated all qualifications-related keys: tab name, section titles, action labels, and error messages.

### Fix 5: Time display with directional arrow (Item 10)
- **`apps/web/components/schedule/ScheduleTaskTable.tsx`** — Changed time display from "start – end" to "start ← end" (RTL/Hebrew) or "start → end" (LTR/English).

### Fix 6: Sticky week/day header (Item 12)
- **`apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx`** — Wrapped week navigation and day tabs in a sticky container (`sticky top-0 z-20 bg-slate-50`).

### Fix 7: Notifications in wrong language (Item 22)
- **`apps/api/Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs`** — The "schedule published" in-app notification now uses the space's locale (he/ru/en) instead of hardcoded English.

## Key decisions
- Used space locale (not user locale) for publish notifications — consistent with how the SolverWorkerService handles locale.
- For the time arrow, used `←` for RTL and `→` for LTR — visually indicates flow direction in both reading orders.
- Onboarding steps are shown inline in the groups page empty state, keeping the floating panel as a secondary reminder.

## How it connects
- Translation changes affect all Hebrew-speaking users immediately.
- The sticky header improves schedule page usability on mobile.
- Locale-aware notifications align with the existing pattern in SolverWorkerService.

## How to run / verify
1. `dotnet build` in `apps/api/` — should pass with 0 errors.
2. Open the groups page with no groups → onboarding steps should appear inline.
3. Check the schedule page → week nav and day tabs should stick when scrolling.
4. Switch to Hebrew → "הרשאות" tab and "הכשרות" tab should show correct names.
5. Publish a schedule in a Hebrew-locale space → notification should be in Hebrew.

## What comes next
- Update the BACKLOG.md to mark these items as done.

## Git commit
```bash
git add -A && git commit -m "fix(ux): 7 naming and UX fixes — branding, i18n, sticky header, locale notifications"
```
