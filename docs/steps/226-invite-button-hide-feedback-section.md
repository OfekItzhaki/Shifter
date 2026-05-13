# 226 — Hide Invite Button for Verified Members & Feedback Section

## Phase

Post-launch polish — small UX fixes batch

## Purpose

1. **Item 4:** The invite button was shown for all non-owner members, even those who already have a linked user account (verified/registered). This is confusing — you shouldn't invite someone who's already in the system.
2. **Item 18 (Vacation workflow):** Confirmed already complete via unavailability reasons, presence windows, solver integration, and stats tracking.
3. **Item 27:** Users need a way to report bugs or send feedback. A simple `mailto:` link on the profile page provides this with zero backend work.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/tabs/MembersTab.tsx` | Added `!m.linkedUserId` condition to only show invite button for unlinked members |
| `apps/web/app/profile/page.tsx` | Added `FeedbackSection` component with `mailto:` link |
| `apps/web/messages/he.json` | Added `feedback`, `feedbackDesc`, `feedbackButton` translation keys |
| `apps/web/messages/en.json` | Added `feedback`, `feedbackDesc`, `feedbackButton` translation keys |

## Key decisions

- Used `linkedUserId` field from `GroupMemberDto` — if non-null, the person already has a registered user account and doesn't need an invitation.
- Feedback uses a simple `mailto:support@shifter.app` link rather than a custom form — works immediately, no backend needed.
- Placed feedback section between "Export Data" and "Delete Account" on the profile page for logical grouping.

## How it connects

- The invite button fix works with the existing invitation flow (specs in `.kiro/specs/invitation-flow-fixes/`)
- The feedback section is self-contained — just a mailto link

## How to run / verify

1. Open a group detail page → Members tab → verify invite button is hidden for members with a linked user account
2. Open profile page → scroll to "דיווח באג / משוב" section → click button → verify email client opens with pre-filled subject
3. Run `npx tsc --noEmit` in `apps/web` to confirm no TypeScript errors

## What comes next

No dependencies — these are standalone polish items.

## Git commit

```bash
git add -A && git commit -m "fix(ux): hide invite for verified members, add feedback mailto button"
```
