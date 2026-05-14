# Product Backlog — Rolduler/Jobuler

Tracked improvements and features from mobile testing session (May 2026).

## Priority 1 — Blocking Bugs (Auth/Invitation)

| # | Item | Status | Spec |
|---|------|--------|------|
| 24 | 403 after joining via invitation link — user doesn't see group | ✅ DONE | `invitation-flow-fixes` |
| 25 | Invitation flow: auto-join if account exists, register page if not, then auto-join after register | ✅ DONE | `invitation-flow-fixes` |
| 26 | Phone-only registration + WhatsApp message support | ✅ DONE (already working) | `invitation-flow-fixes` |
| 23 | 401 error page → redirect to landing page instead | ✅ DONE | `invitation-flow-fixes` |

## Priority 2 — Mobile UI Bugs

| # | Item | Status | Spec |
|---|------|--------|------|
| 1 | Statistics page stuck when no data (loading spinner forever) | ✅ DONE | |
| 9 | 24h time format not working on mobile | ✅ DONE | |
| 21 | Sliders white dot not visible on mobile | ✅ DONE | |

## Priority 3 — Small UX/Naming Fixes (Batch)

| # | Item | Status | Spec |
|---|------|--------|------|
| 2 | Move starter instructions into "My Groups" tab | ✅ DONE | |
| 3 | Email content says "Jobular" — fix branding to correct name | ✅ DONE | |
| 5 | Rename "Roles" → "רמת הרשאות" or "הרשאות קבוצה" (shorter) | ✅ DONE | |
| 6 | Change "כישורים" → "הכשרות" in Hebrew | ✅ DONE | |
| 10 | Time in schedule: show (start) → (end) with RTL/LTR arrow | ✅ DONE | |
| 12 | Sticky week/day header when scrolling schedule page | ✅ DONE | |
| 22 | Notifications in wrong language (English instead of Hebrew) | ✅ DONE | |

## Priority 4 — Templates System (Qualifications + Unavailability)

| # | Item | Status | Spec |
|---|------|--------|------|
| 7 | Qualifications from templates (Army, Restaurant, etc.) with editable defaults | ✅ DONE | `qualification-templates` |
| 8 | Unavailability window: title/reason from template list + custom reason option | ✅ DONE | `qualification-templates` |

## Priority 5 — Color-Coded Roles/Units

| # | Item | Status | Spec |
|---|------|--------|------|
| 11 | Color support for role types — admin assigns colors to roles, members shown in role color. Army=units, Restaurant=stations | ✅ DONE | `color-coded-roles` |

## Priority 6 — Statistics Overhaul + Burden Levels

| # | Item | Status | Spec |
|---|------|--------|------|
| 15 | Statistics: track who's getting screwed, vacations, home time, sickness | ✅ DONE | `statistics-overhaul` |
| 16 | Statistics: color missions by difficulty (red=hard, gray=normal, green=easy) | ✅ DONE | `statistics-overhaul` |
| 17 | Change burden levels: Hard/Normal/Easy instead of Hated/Disliked/Neutral/Liked | ✅ DONE | `statistics-overhaul` |
| 19 | Statistics as graphs instead of tables | ✅ DONE | `statistics-overhaul` |
| 20 | Task rotation tracking (army template — iterate through all tasks in loop) | ✅ DONE | `statistics-overhaul` |

## Priority 7 — Vacation/Special Occasion Workflow

| # | Item | Status | Spec |
|---|------|--------|------|
| 18 | Vacation/Special occasion workflow (request, approve, block from schedule) | ✅ DONE (via unavailability reasons) | `qualification-templates` |

## Priority 8 — Invitation UX + Schedule Diff

| # | Item | Status | Spec |
|---|------|--------|------|
| 4 | Hide invite button if person already in group | ✅ DONE | |
| 13 | Show changes between last published schedule and current draft | ✅ DONE (ScheduleDiffView exists) | |
| 14 | Schedule change history — verify it works end-to-end | ✅ DONE (ScheduleHistory exists) | |

## Priority 9 — Feedback Mechanism

| # | Item | Status | Spec |
|---|------|--------|------|
| 27 | Bug report / feedback button for users | ✅ DONE | |

## Priority 10 — Biometric Login (Future)

| # | Item | Status | Spec |
|---|------|--------|------|
| 28 | Login with Biometrics (fingerprint/face) | ✅ DONE | `biometric-login` |

---

## Completed Specs

| Feature | Spec Path | Status |
|---------|-----------|--------|
| Home-Leave Scheduling | `.kiro/specs/home-leave-scheduling/` | ✅ Implemented |
| Push Notifications | `.kiro/specs/push-notifications/` | ✅ Implemented |
