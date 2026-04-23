# Step 027 — Group Detail Page, AppShell Nav Restructure, Seed UUID Randomization

## Phase
Phase 8 — Group Detail Page Feature

## Purpose
Three coordinated changes that complete the per-group admin model:

1. **Group Detail Page** — replaces the placeholder at `/groups/[groupId]` with a full tabbed UI. All members see a schedule tab and a read-only members tab. Users who activate admin mode for that specific group gain four additional management tabs (member editing, task types/slots, constraints, solver settings).

2. **AppShell Navigation Restructure** — removes the global "Admin" sidebar section and the global admin-mode toggle from the topbar. Navigation simplifies to סידור sub-items and קבוצות. The amber topbar indicator is kept but now driven by `adminGroupId !== null` (per-group scope).

3. **Seed UUID Randomization** — sequential fake UUIDs in `seed.sql` are replaced with random-looking UUID v4 values to avoid accidental collisions and better reflect production data.

## What was built

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/page.tsx` | Full group detail page: tab bar, schedule panel, members read-only panel, members edit panel (add/remove), tasks panel (types + slots sub-tabs), constraints panel, settings panel (solver horizon slider) |
| `apps/web/components/shell/AppShell.tsx` | Removed Admin sidebar section, removed global admin toggle and badge from topbar, added קבוצות nav item, topbar now driven by `adminGroupId !== null` |
| `infra/scripts/seed.sql` | All 26 sequential UUIDs replaced with random-looking UUID v4 values; UUID mapping comment block added at top |
| `apps/web/__tests__/group-detail-tabs.test.ts` | Pure logic property tests: base tabs always present, admin tabs conditional, displayName fallback, members re-fetch pattern, solver horizon warning threshold, no Admin section in nav, amber topbar, seed UUID validity |

## Key decisions

- **Per-group admin mode** — `adminGroupId` in `authStore` is scoped to a specific group. Entering admin mode on one group does not affect other groups. The mode is never persisted across page loads (handled by `partialize` in the store).
- **Tab conditional rendering** — admin-only tabs (`members-edit`, `tasks`, `constraints`, `settings`) are only added to the visible tab array when `adminGroupId === groupId`. A `useEffect` on `adminGroupId` resets `activeTab` to `"schedule"` if the user exits admin mode while on an admin-only tab.
- **Amber topbar indicator** — `S.topbar(admin)` is now driven by `adminGroupId !== null` instead of the removed `isAdminMode` getter. This preserves the visual cue that admin mode is active somewhere, without requiring a global toggle.
- **Seed UUID randomization rationale** — sequential UUIDs like `00000000-0000-0000-0000-000000000001` are visually obvious as fake data and can cause accidental collisions if copy-pasted into other environments. Random-looking v4 UUIDs are safer and more realistic.
- **AppShell uses inline styles** — the `S` object pattern is preserved; no Tailwind classes added to AppShell. The `adminBtn` and `adminBadge` style entries were removed since they are no longer rendered.
- **Existing `/admin/*` pages untouched** — the admin pages remain in the codebase for direct URL access; only the sidebar navigation links to them were removed.

## How it connects

- `GroupDetailPage` reads `adminGroupId` from `authStore` and `currentSpaceId` from `spaceStore` — the same stores used by all other pages.
- The `getGroups`, `getGroupMembers`, `addGroupMemberByEmail`, `removeGroupMember`, and `updateGroupSettings` functions were already added to `lib/api/groups.ts` in task 1.
- The tasks and constraints panels reuse the same display logic (table structure, `burdenLabels`/`burdenColors`, `SEVERITY_STYLES`/`SEVERITY_DOTS`) as the existing `/admin/tasks` and `/admin/constraints` pages.
- The seed UUID changes affect the local development database only. All foreign-key relationships are preserved — every UUID used as a FK reference is also defined as a PK in the same file.

## How to run / verify

1. Start the frontend: `cd apps/web && npm run dev`
2. Navigate to `/groups` — the קבוצות nav item should appear in the sidebar.
3. Click a group card — the group detail page opens with "סידור" and "חברים" tabs.
4. Click "כניסה למצב מנהל" — four additional tabs appear; the topbar turns amber.
5. Click "יציאה ממצב מנהל" — admin tabs disappear; topbar returns to white.
6. Navigate away from the group page — admin mode is cleared (topbar returns to white).
7. Verify the AppShell sidebar has no "Admin" section and no `/admin/*` links.
8. Re-seed the database: `psql -U postgres -d jobuler_dev -f infra/scripts/seed.sql` — should run without errors.
9. Run the property tests (requires `ts-node`):
   ```bash
   cd apps/web
   node --require ts-node/register __tests__/group-detail-tabs.test.ts
   ```

## What comes next

- Add a confirmation dialog before removing a group member.
- Add pagination or search to the members list for large groups.
- Connect the schedule tab to a real published schedule version (currently fetches from `/schedule` endpoint).
- Consider adding a "trigger solver" button in the settings tab for group admins.
- Add E2E tests for the group detail page flow using Playwright.

## Git commit
```bash
git add -A && git commit -m "feat(phase8): group detail page, AppShell nav restructure, seed UUID randomization"
```
