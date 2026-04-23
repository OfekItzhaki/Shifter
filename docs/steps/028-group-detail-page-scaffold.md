# Step 028 — Group Detail Page Scaffold (Task 2.1)

## Phase
Phase 8 — Group Detail Page Feature

## Purpose
Replace the placeholder file at `apps/web/app/groups/[groupId]/page.tsx` with a real Next.js client component that fetches the group, renders the header, and holds all state declarations needed by subsequent tasks (tab bar, admin toggle, member management, tasks, constraints, settings).

## What was built

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/page.tsx` | Replaced "placeholder" with full scaffold: `"use client"`, all imports, all state declarations, group fetch `useEffect`, loading/not-found/header render |

### Key implementation details

- `type ActiveTab` union covers all six tab values: `"schedule" | "members-readonly" | "members-edit" | "tasks" | "constraints" | "settings"`
- All 16 state variables declared upfront so subsequent tasks can add logic without restructuring
- `useEffect` on `currentSpaceId` calls `getGroups`, finds the group by `groupId`, sets `group` + `solverHorizon`, or sets `notFound`; always calls `setLoading(false)`
- Loading state renders `<p>טוען...</p>`; not-found state renders centered heading + back link; found state renders the header with group name and member count
- Admin toggle and tab bar are left as comments/placeholder `<div>` — added in tasks 2.3 and 3.1

## Key decisions

- All state is declared at the top of the component even though most of it is unused in this task — this avoids restructuring the component in every subsequent task and matches the design doc's state table exactly.
- `useRouter` is imported but not yet used; it will be needed in later tasks (e.g., redirect on auth failure).
- Error on group fetch falls back to `setNotFound(true)` rather than a separate error state, keeping the UX simple for the not-found case.

## How it connects

- Depends on task 1 (`lib/api/groups.ts`) for `getGroups`, `GroupWithMemberCountDto`, `GroupMemberDto`, and the other API functions.
- Task 2.3 adds the admin toggle button into the `{/* admin toggle */}` comment slot.
- Task 3.1 replaces `<div>tabs go here</div>` with the real tab bar.
- Tasks 3.4, 3.5, 5.1–5.5 fill in the tab panel logic using the state already declared here.

## How to run / verify

1. Start the dev server: `npm run dev` (from `apps/web`)
2. Navigate to `/groups` and click any group card
3. The page should show the group name and member count in the header
4. Navigate to `/groups/nonexistent-id` — should show "קבוצה לא נמצאה" with a back link

## What comes next

- Task 2.2 — property test for group lookup correctness
- Task 2.3 — admin toggle button and `useEffect` cleanup

## Git commit

```bash
git add -A && git commit -m "feat(group-detail): scaffold page with group fetch and header (task 2.1)"
```
