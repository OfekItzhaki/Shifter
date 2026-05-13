# 218 — Color-Coded Roles Frontend

## Phase

Feature — Color-Coded Roles (Frontend)

## Purpose

Adds the frontend layer for the color-coded roles feature: API client types, a color picker component, integration into the role form, and color indicators in schedule views and member lists.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/groups.ts` | Added `color: string \| null` to `GroupRoleDto`; updated `createGroupRole` and `updateGroupRole` payload types to include optional `color` |
| `apps/web/components/RoleColorPicker.tsx` | New component — 8 preset color circles with select/deselect behavior and ring indicator |
| `apps/web/app/groups/[groupId]/tabs/RolesTab.tsx` | Integrated `RoleColorPicker` into create-role and edit-role forms; updated callback signatures to pass color; added colored dot before role badge in member list |
| `apps/web/app/groups/[groupId]/page.tsx` | Updated `handleCreateRole` and `handleUpdateRole` to accept and pass `color` to API |
| `apps/web/lib/utils/roleColorMap.ts` | New utility — `buildRoleColorMap(members, roles)` returns `Map<personId, roleColor>` |
| `apps/web/components/schedule/ScheduleTaskTable.tsx` | Added optional `roleColorMap` prop; renders 3px left border on person name when color exists |
| `apps/web/components/schedule/ScheduleTable2D.tsx` | Added optional `roleColorMap` prop; extended `TableAssignment` with optional `personId`; renders 3px left border on person name div |

## Key decisions

- Used inline styles for color indicators since dynamic hex values can't be handled by Tailwind's JIT
- The `roleColorMap` prop is optional on schedule components — existing usages without role data continue to work unchanged
- Extended `TableAssignment` type with optional `personId` (backward-compatible) rather than requiring it
- Color picker uses a fixed palette of 8 colors matching the design spec — no custom color input
- Clicking a selected color deselects it (sets to null), matching the "no color" requirement

## How it connects

- Backend (step 217) already persists and returns the `color` field on roles
- The `buildRoleColorMap` utility is ready to be called from any parent component that has access to members and roles data
- Schedule components accept the map as an optional prop — parent pages can pass it when role data is available

## How to run / verify

1. Open a group → Roles tab → create/edit a role → color picker should appear below permission level
2. Assign a color → save → verify the API request includes `color` field
3. In the member list section, members with colored roles should show a small dot before the role badge
4. Schedule views that pass `roleColorMap` will show left-border indicators on person names

## What comes next

- Wire `buildRoleColorMap` into the ScheduleTab and admin schedule pages to pass the prop to schedule components
- Task 5.5 (optional): Property-based test for color indicator rendering consistency

## Git commit

```bash
git add -A && git commit -m "feat(color-roles): frontend color picker, API types, and schedule indicators"
```
