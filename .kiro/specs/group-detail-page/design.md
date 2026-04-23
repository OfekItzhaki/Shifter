# Design Document — group-detail-page

## Overview

This feature delivers three coordinated changes:

1. **Group Detail Page** (`/groups/[groupId]/page.tsx`) — a full tabbed page replacing the current placeholder. All members see a schedule tab and a read-only members tab. Users who activate admin mode for that specific group gain four additional management tabs.

2. **AppShell Navigation Restructure** (`apps/web/components/shell/AppShell.tsx`) — the global "Admin" sidebar section and the global admin-mode toggle are removed. Navigation simplifies to סידור sub-items and קבוצות. The amber topbar indicator is kept but now driven by `adminGroupId !== null`.

3. **Seed UUID Randomization** (`infra/scripts/seed.sql`) — sequential fake UUIDs are replaced with random-looking UUID v4 values. All foreign-key references are updated consistently.

Admin mode remains frontend-only state. All write operations are gated server-side by `IPermissionService`. `adminGroupId` is never persisted across page loads (already handled by `authStore` `partialize`).

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  /groups/[groupId]/page.tsx  (GroupDetailPage)              │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Header: group name + memberCount + admin toggle     │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Tab bar                                             │   │
│  │  Always: [סידור] [חברים]                             │   │
│  │  Admin:  [חברים✎] [משימות] [אילוצים] [הגדרות]       │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Active tab panel                                    │   │
│  │  ScheduleTab | MembersTab | TasksTab |               │   │
│  │  ConstraintsTab | SettingsTab                        │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘

State sources:
  authStore  → adminGroupId, enterAdminMode, exitAdminMode
  spaceStore → currentSpaceId
  local useState → activeTab, group, members, loading, error
```

### Admin mode lifecycle

```
Page mounts
  └─ useEffect: return () => exitAdminMode()   ← cleanup on unmount

User clicks "כניסה למצב מנהל"
  └─ enterAdminMode(groupId)
  └─ adminGroupId === groupId → admin tabs appear

User clicks "יציאה ממצב מנהל"
  └─ exitAdminMode()
  └─ adminGroupId === null → admin tabs disappear

User navigates away
  └─ useEffect cleanup fires → exitAdminMode()
```

---

## Components and Interfaces

### Files created

| File | Description |
|------|-------------|
| `apps/web/app/groups/[groupId]/page.tsx` | Full group detail page (replaces placeholder) |

### Files modified

| File | Change |
|------|--------|
| `apps/web/components/shell/AppShell.tsx` | Remove Admin section, remove global toggle, add קבוצות nav item |
| `apps/web/lib/api/groups.ts` | Add `getGroups`, `getGroupMembers`, `addGroupMemberByEmail`, `removeGroupMember`, `updateGroupSettings` |
| `infra/scripts/seed.sql` | Replace sequential UUIDs with random-looking UUID v4 values |

### New API client functions — `lib/api/groups.ts`

```typescript
// DTOs
export interface GroupWithMemberCountDto {
  id: string;
  name: string;
  memberCount: number;
  solverHorizonDays: number;
}

export interface GroupMemberDto {
  personId: string;
  fullName: string;
  displayName: string | null;
}

// New functions
export async function getGroups(spaceId: string): Promise<GroupWithMemberCountDto[]>
export async function getGroupMembers(spaceId: string, groupId: string): Promise<GroupMemberDto[]>
export async function addGroupMemberByEmail(spaceId: string, groupId: string, email: string): Promise<void>
export async function removeGroupMember(spaceId: string, groupId: string, personId: string): Promise<void>
export async function updateGroupSettings(spaceId: string, groupId: string, solverHorizonDays: number): Promise<void>
```

### GroupDetailPage — internal tab types

```typescript
type BaseTab = "schedule" | "members-readonly";
type AdminTab = "members-edit" | "tasks" | "constraints" | "settings";
type ActiveTab = BaseTab | AdminTab;
```

When `adminGroupId === groupId`, the active tab defaults to `"schedule"` and the full tab set is available. When admin mode exits, if the current tab is an admin-only tab, reset to `"schedule"`.

### AppShell changes (summary)

**Remove:**
- `isAdminMode` destructure (replace with `adminGroupId !== null` inline)
- The `{isAdminMode && <> Admin section </>}` block in `<nav>`
- The `enterAdminMode` button in the topbar
- The `exitAdminMode` button in the topbar
- The `adminBadge` div in the topbar
- The `adminBtn` style entry in `S` (no longer needed)

**Keep:**
- `S.topbar(admin)` — driven by `adminGroupId !== null` instead of `isAdminMode`
- `NotificationBell`
- Logout button
- All existing schedule nav items

**Add:**
- `<NavItem href="/groups" label="קבוצות" icon={...} />` below the סידור section

---

## Data Models

### GroupWithMemberCountDto (frontend)

```typescript
interface GroupWithMemberCountDto {
  id: string;
  name: string;
  memberCount: number;
  solverHorizonDays: number;
}
```

Source: `GET /spaces/{spaceId}/groups` — already returns this shape (confirmed from `groups/page.tsx`).

### GroupMemberDto (frontend)

```typescript
interface GroupMemberDto {
  personId: string;
  fullName: string;
  displayName: string | null;
}
```

Source: `GET /spaces/{spaceId}/groups/{groupId}/members`

Display rule: `member.displayName ?? member.fullName`

### GroupDetailPage local state

```typescript
const [group, setGroup] = useState<GroupWithMemberCountDto | null>(null);
const [notFound, setNotFound] = useState(false);
const [members, setMembers] = useState<GroupMemberDto[]>([]);
const [activeTab, setActiveTab] = useState<ActiveTab>("schedule");
const [loading, setLoading] = useState(true);
const [membersLoading, setMembersLoading] = useState(false);
const [addEmail, setAddEmail] = useState("");
const [addError, setAddError] = useState<string | null>(null);
const [settingsError, setSettingsError] = useState<string | null>(null);
const [solverHorizon, setSolverHorizon] = useState(14);
const [savingSettings, setSavingSettings] = useState(false);
```

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Group lookup correctness

*For any* list of groups and any groupId, the lookup function returns the group whose `id` equals `groupId`, and the rendered header displays that group's `name` and `memberCount`. If no group matches, the result is `null`.

**Validates: Requirements 1.1, 1.2**

### Property 2: Base tabs always present

*For any* value of `adminGroupId` (null, equal to groupId, or any other string), the GroupDetailPage SHALL render both the "סידור" tab and the "חברים" tab.

**Validates: Requirements 2.1**

### Property 3: DisplayName fallback

*For any* `GroupMemberDto`, the display string is `displayName` when `displayName` is non-null, and `fullName` when `displayName` is null.

**Validates: Requirements 2.4**

### Property 4: Admin tabs appear exactly when adminGroupId matches

*For any* groupId, when `adminGroupId === groupId`, the four admin tabs ("חברים" edit, "משימות", "אילוצים", "הגדרות") are all rendered. When `adminGroupId !== groupId` (including null), none of those four tabs are rendered.

**Validates: Requirements 3.1**

### Property 5: Members list re-fetched after any mutation

*For any* successful add-member or remove-member operation, the `getGroupMembers` API function is called again after the operation completes, ensuring the displayed list reflects the updated state.

**Validates: Requirements 3.6**

### Property 6: Solver horizon warning threshold

*For any* slider value `v` in the range 1–90: if `v > 30`, the complexity warning message is displayed; if `v ≤ 30`, the warning is not displayed.

**Validates: Requirements 3.10**

### Property 7: No Admin section in AppShell for any adminGroupId

*For any* value of `adminGroupId` (null or any non-null string), the AppShell sidebar SHALL NOT render an "Admin" section or any `/admin/*` nav links.

**Validates: Requirements 4.3**

### Property 8: Amber topbar when adminGroupId is non-null

*For any* non-null value of `adminGroupId`, the AppShell topbar background is `#fffbeb` (amber). When `adminGroupId` is null, the topbar background is white.

**Validates: Requirements 4.6**

### Property 9: Seed UUID validity and FK integrity

*For any* UUID value appearing in `seed.sql`, it matches the UUID v4 format regex (`[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}`). Additionally, every UUID used as a foreign key reference in the file also appears as a primary key definition in the same file.

**Validates: Requirements 5.1, 5.2**

### Property 10: Seed idempotence

*For any* already-seeded database, running `seed.sql` a second time produces no errors — all `ON CONFLICT` clauses handle duplicate rows gracefully.

**Validates: Requirements 5.4**

---

## Error Handling

### GroupDetailPage

| Scenario | Handling |
|----------|----------|
| Group not found in list | Show "קבוצה לא נמצאה" + back link to `/groups` |
| `currentSpaceId` is null | Show loading state; space auto-resolves via AppShell |
| Add-member API error | Display `err?.response?.data?.message` below the email input |
| Remove-member API error | Display error inline near the member row |
| Settings save API error | Display `err?.response?.data?.message` below the save button |
| Schedule fetch error | Display "שגיאה בטעינת הסידור" in the schedule tab |
| Members fetch error | Display "שגיאה בטעינת החברים" in the members tab |

### AppShell

No new error states introduced. The `adminGroupId !== null` check is a pure boolean — no async operations.

### Seed SQL

The `ON CONFLICT DO NOTHING` and `ON CONFLICT DO UPDATE` clauses already present in the file are preserved unchanged. The UUID mapping comment block at the top of the file documents the old→new mapping for developer reference.

---

## Testing Strategy

### Unit / example-based tests

Focus on specific behaviors and edge cases:

- GroupDetailPage renders "קבוצה לא נמצאה" when groupId is not in the groups list
- Admin toggle button label switches correctly based on `adminGroupId`
- `exitAdminMode` is called on component unmount
- Members tab shows read-only list when not in admin mode
- Members tab shows add-email form and remove buttons when in admin mode
- Empty members list shows "אין חברים בקבוצה זו"
- Settings tab shows slider with range 1–90
- AppShell renders קבוצות nav item
- AppShell does not render any `/admin/*` links
- AppShell does not render admin toggle button or badge

### Property-based tests

Use a property-based testing library (e.g., `fast-check` for TypeScript/Jest). Each property test runs a minimum of 100 iterations.

Tag format: `Feature: group-detail-page, Property {N}: {property_text}`

| Property | Generator inputs | Assertion |
|----------|-----------------|-----------|
| P1: Group lookup | `fc.array(groupDtoArb)`, `fc.string()` | `findGroup(list, id)?.id === id` or null |
| P2: Base tabs always present | `fc.option(fc.string())` as adminGroupId | Both tab labels present in rendered output |
| P3: DisplayName fallback | `fc.record({ displayName: fc.option(fc.string()), fullName: fc.string() })` | `getDisplayName(m) === m.displayName ?? m.fullName` |
| P4: Admin tabs conditional | `fc.string()` as groupId, `fc.option(fc.string())` as adminGroupId | Admin tabs present iff `adminGroupId === groupId` |
| P5: Re-fetch after mutation | Mock add/remove, any member data | `getGroupMembers` called after each successful mutation |
| P6: Warning threshold | `fc.integer({ min: 1, max: 90 })` | Warning shown iff `value > 30` |
| P7: No Admin section | `fc.option(fc.string())` as adminGroupId | No Admin section in rendered AppShell |
| P8: Amber topbar | `fc.option(fc.string())` as adminGroupId | Topbar style matches amber iff non-null |
| P9: UUID validity | Parse seed.sql statically | All UUIDs match v4 regex; all FK refs resolve |
| P10: Seed idempotence | Integration — run seed.sql twice on test DB | No errors on second run |

---

## Seed UUID Mapping Table

The following table maps old sequential UUIDs to new random-looking UUID v4 values. This mapping will appear as a comment block at the top of `seed.sql`.

| Entity | Old UUID | New UUID |
|--------|----------|----------|
| User: admin | `00000000-0000-0000-0000-000000000001` | `a1b2c3d4-e5f6-4a7b-8c9d-e0f1a2b3c4d5` |
| User: ofek | `00000000-0000-0000-0000-000000000002` | `b2c3d4e5-f6a7-4b8c-9d0e-f1a2b3c4d5e6` |
| User: yael | `00000000-0000-0000-0000-000000000003` | `c3d4e5f6-a7b8-4c9d-0e1f-a2b3c4d5e6f7` |
| User: viewer | `00000000-0000-0000-0000-000000000004` | `d4e5f6a7-b8c9-4d0e-1f2a-b3c4d5e6f7a8` |
| Space: Unit Alpha | `10000000-0000-0000-0000-000000000001` | `e5f6a7b8-c9d0-4e1f-2a3b-c4d5e6f7a8b9` |
| Role: Soldier | `20000000-0000-0000-0000-000000000001` | `f6a7b8c9-d0e1-4f2a-3b4c-d5e6f7a8b9c0` |
| Role: Squad Commander | `20000000-0000-0000-0000-000000000002` | `a7b8c9d0-e1f2-4a3b-4c5d-e6f7a8b9c0d1` |
| Role: Medic | `20000000-0000-0000-0000-000000000003` | `b8c9d0e1-f2a3-4b4c-5d6e-f7a8b9c0d1e2` |
| Role: Duty Officer | `20000000-0000-0000-0000-000000000004` | `c9d0e1f2-a3b4-4c5d-6e7f-a8b9c0d1e2f3` |
| GroupType: Squad | `30000000-0000-0000-0000-000000000001` | `d0e1f2a3-b4c5-4d6e-7f8a-b9c0d1e2f3a4` |
| GroupType: Platoon | `30000000-0000-0000-0000-000000000002` | `e1f2a3b4-c5d6-4e7f-8a9b-c0d1e2f3a4b5` |
| Group: Squad A | `40000000-0000-0000-0000-000000000001` | `f2a3b4c5-d6e7-4f8a-9b0c-d1e2f3a4b5c6` |
| Group: Squad B | `40000000-0000-0000-0000-000000000002` | `a3b4c5d6-e7f8-4a9b-0c1d-e2f3a4b5c6d7` |
| Person: Ofek | `50000000-0000-0000-0000-000000000001` | `b4c5d6e7-f8a9-4b0c-1d2e-f3a4b5c6d7e8` |
| Person: Yael | `50000000-0000-0000-0000-000000000002` | `c5d6e7f8-a9b0-4c1d-2e3f-a4b5c6d7e8f9` |
| Person: Daniel | `50000000-0000-0000-0000-000000000003` | `d6e7f8a9-b0c1-4d2e-3f4a-b5c6d7e8f9a0` |
| Person: Michal | `50000000-0000-0000-0000-000000000004` | `e7f8a9b0-c1d2-4e3f-4a5b-c6d7e8f9a0b1` |
| Person: Ron | `50000000-0000-0000-0000-000000000005` | `f8a9b0c1-d2e3-4f4a-5b6c-d7e8f9a0b1c2` |
| Person: Noa | `50000000-0000-0000-0000-000000000006` | `a9b0c1d2-e3f4-4a5b-6c7d-e8f9a0b1c2d3` |
| TaskType: Post 1 | `60000000-0000-0000-0000-000000000001` | `b0c1d2e3-f4a5-4b6c-7d8e-f9a0b1c2d3e4` |
| TaskType: Post 2 | `60000000-0000-0000-0000-000000000002` | `c1d2e3f4-a5b6-4c7d-8e9f-a0b1c2d3e4f5` |
| TaskType: Kitchen | `60000000-0000-0000-0000-000000000003` | `d2e3f4a5-b6c7-4d8e-9f0a-b1c2d3e4f5a6` |
| TaskType: War Room | `60000000-0000-0000-0000-000000000004` | `e3f4a5b6-c7d8-4e9f-0a1b-c2d3e4f5a6b7` |
| TaskType: Patrol | `60000000-0000-0000-0000-000000000005` | `f4a5b6c7-d8e9-4f0a-1b2c-d3e4f5a6b7c8` |
| TaskType: Reserve | `60000000-0000-0000-0000-000000000006` | `a5b6c7d8-e9f0-4a1b-2c3d-e4f5a6b7c8d9` |
