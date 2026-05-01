# Design Document — Personal and Role Constraints

## Overview

This feature surfaces the personal (`ScopeType.Person`) and role-based (`ScopeType.Role`) constraint capabilities that already exist in the backend domain but are not yet exposed in the UI. The work splits into two thin layers:

1. **Backend guard** — `CreateConstraintCommandHandler` already validates person existence and the registered-member rule (added in the previous spec). This design confirms that implementation is complete and specifies the exact HTTP status codes and error messages the frontend must handle.

2. **Frontend restructure** — `ConstraintsTab` is refactored from a single flat list into three collapsible sections (Group / Personal / Role), each with its own inline create form. The component already contains a partial implementation (`SectionCreateForm`, `onCreateWithScope`) that this feature completes and stabilises.

No new API endpoints are required. All reads go through the existing `GET /spaces/{spaceId}/constraints`, and all writes go through the existing `POST`, `PUT`, and `DELETE` endpoints on `ConstraintsController`.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Next.js Frontend                                               │
│                                                                 │
│  GroupDetailPage (page.tsx)                                     │
│    ├─ loads constraints on tab activation                       │
│    ├─ loads groupRoles on constraints tab activation (new)      │
│    └─ ConstraintsTab                                            │
│         ├─ ConstraintSection "אילוצי קבוצה"                    │
│         │    └─ SectionCreateForm (scopeType=group)             │
│         ├─ ConstraintSection "אילוצים אישיים"                  │
│         │    └─ SectionCreateForm (scopeType=person)            │
│         │         └─ person selector (registered members only)  │
│         └─ ConstraintSection "אילוצי תפקיד"                    │
│              └─ SectionCreateForm (scopeType=role)              │
│                   └─ role selector (active roles only)          │
└─────────────────────────────────────────────────────────────────┘
                          │  HTTP (existing endpoints)
┌─────────────────────────────────────────────────────────────────┐
│  ASP.NET Core API                                               │
│                                                                 │
│  ConstraintsController                                          │
│    GET  /spaces/{id}/constraints  → GetConstraintsQuery         │
│    POST /spaces/{id}/constraints  → CreateConstraintCommand     │
│    PUT  /spaces/{id}/constraints/{cid}                          │
│    DELETE /spaces/{id}/constraints/{cid}                        │
│                                                                 │
│  CreateConstraintCommandHandler                                 │
│    1. RequirePermissionAsync (constraints.manage)               │
│    2. Role scope: verify SpaceRole exists & is active           │
│    3. Person scope: verify Person exists in space               │
│    4. Person scope: verify linked_user_id != null               │
│                     AND invitation_status = "accepted"          │
│    5. ConstraintRule.Create → SaveChangesAsync                  │
└─────────────────────────────────────────────────────────────────┘
```

### Key architectural decisions

- **No new endpoints.** The existing `GET /spaces/{spaceId}/constraints` already returns all scope types. The frontend partitions the flat list client-side by `scopeType`.
- **Role loading moved to constraints tab.** `groupRoles` are currently loaded only when the settings tab opens. The `page.tsx` effect must be extended to also load roles when `activeTab === "constraints"`.
- **Registered-member guard is backend-only.** The frontend filters the person selector as a UX convenience, but the backend enforces the rule independently (HTTP 422 for unregistered persons).
- **Inline forms, not modals.** The existing `SectionCreateForm` pattern (inline expand/collapse within each section) is retained. The legacy modal path (`showConstraintForm` / `onCreateSubmit`) is kept for backward compatibility but is no longer the primary path.

---

## Components and Interfaces

### Backend — no new files

The `CreateConstraintCommandHandler` already contains the full validation chain. No new commands, queries, validators, or controllers are needed.

**Error contract** (for frontend error handling):

| Condition | HTTP status | Message |
|---|---|---|
| `scope_id` null/empty when `scope_type = person` | 400 | FluentValidation message |
| Person not found in space | 404 | "Person not found in this space." |
| Person not registered (`linked_user_id` null or `invitation_status != "accepted"`) | 422 | "Personal constraints can only be applied to registered members." |
| `scope_id` null/empty when `scope_type = role` | 400 | FluentValidation message |
| Role not found or inactive | 404 | "Role not found in this space." |

### Frontend — modified files

#### `apps/web/app/groups/[groupId]/page.tsx`

**Change:** Extend the constraints `useEffect` to also load `groupRoles` when the constraints tab is first activated.

```typescript
// Before (loads constraints only):
useEffect(() => {
  if (!currentSpaceId || !groupId || activeTab !== "constraints") return;
  setConstraintsLoading(true);
  getConstraints(currentSpaceId).then(setConstraints)...
}, [currentSpaceId, groupId, activeTab]);

// After (loads constraints + roles together):
useEffect(() => {
  if (!currentSpaceId || !groupId || activeTab !== "constraints") return;
  setConstraintsLoading(true);
  Promise.all([
    getConstraints(currentSpaceId),
    groupRoles.length === 0
      ? getGroupRoles(currentSpaceId, groupId)
      : Promise.resolve(groupRoles),
  ]).then(([c, r]) => { setConstraints(c); setGroupRoles(r); })
    .catch(() => {})
    .finally(() => setConstraintsLoading(false));
}, [currentSpaceId, groupId, activeTab]);
```

**Change:** The `handleCreateConstraint` legacy handler currently filters `setConstraints(updated.filter(c => c.scopeId === groupId))` — this must be changed to `setConstraints(updated)` so personal and role constraints are not stripped.

**Change:** The `onCreateWithScope` callback already exists and is wired correctly. No changes needed there.

#### `apps/web/app/groups/[groupId]/tabs/ConstraintsTab.tsx`

The component already has the three-section structure, `SectionCreateForm`, and `ConstraintRow` with `roleName`/`personName` support. The following gaps must be closed:

1. **Delete confirmation dialog** — `onDeleteConstraint` currently calls the API immediately. A confirmation step (Hebrew text) must be added before the API call. This can be implemented as a `window.confirm` or a small inline confirmation state within `ConstraintRow`.

2. **`GroupMemberDto` does not expose `linkedUserId`** — the `registeredMembers` filter in `SectionCreateForm` uses `m.invitationStatus === "accepted"` as a proxy. This is correct because `invitation_status = "accepted"` implies `linked_user_id != null` by the domain invariant (`Person.LinkUser` sets both). No DTO change is needed.

3. **Section-level error isolation** — the current `sectionSaving` / `sectionError` state is shared across all three sections. Each `SectionCreateForm` should manage its own saving/error state internally (already done — the state is inside `SectionCreateForm`). The parent's `sectionSaving`/`sectionError` can be removed.

4. **Edit form scope display** — when the edit modal opens for a personal or role constraint, the form should display the person/role name as a read-only label (not an editable field). This requires passing `roleMap` and `memberMap` into the edit modal render path.

#### `apps/web/lib/api/groups.ts`

No changes needed. `getGroupRoles` already exists and returns `GroupRoleDto[]` with `id`, `name`, `description`, `isActive`.

#### `apps/web/lib/api/constraints.ts`

No changes needed. `createConstraint` already accepts `scopeType: string` and `scopeId: string | null`.

---

## Data Models

### `ConstraintRule` (existing, no changes)

```
constraint_rules
  id              uuid PK
  space_id        uuid FK → spaces
  scope_type      enum (Person, Role, Group, TaskType, Space)
  scope_id        uuid nullable  -- personId | roleId | groupId | taskTypeId
  severity        enum (Hard, Soft, Emergency)
  rule_type       varchar(100)
  rule_payload_json text
  is_active       bool default true
  effective_from  date nullable
  effective_until date nullable
  created_by_user_id uuid nullable
  updated_by_user_id uuid nullable
  created_at      timestamptz
  updated_at      timestamptz
```

### `Person` (existing, relevant fields)

```
people
  id                  uuid PK
  space_id            uuid FK → spaces
  linked_user_id      uuid nullable  -- null = unregistered
  invitation_status   varchar        -- "pending" | "accepted"
  full_name           varchar
  display_name        varchar nullable
  is_active           bool
```

### `SpaceRole` (existing, relevant fields)

```
space_roles
  id              uuid PK
  space_id        uuid FK → spaces
  group_id        uuid nullable  -- null = space-level role
  name            varchar
  is_active       bool default true
```

### Frontend DTOs (existing, no changes)

```typescript
// ConstraintDto — already includes scopeType and scopeId
interface ConstraintDto {
  id: string;
  scopeType: string;   // "Person" | "Role" | "Group" | ...
  scopeId: string | null;
  severity: string;
  ruleType: string;
  rulePayloadJson: string;
  isActive: boolean;
  effectiveFrom: string | null;
  effectiveUntil: string | null;
}

// GroupMemberDto — invitationStatus used as registered-member proxy
interface GroupMemberDto {
  personId: string;
  fullName: string;
  displayName: string | null;
  invitationStatus: string;   // "accepted" = registered
  ...
}

// GroupRoleDto — isActive used to filter role selector
interface GroupRoleDto {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
}
```

---

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Personal constraint row contains required display fields

*For any* personal constraint and any member list that contains a matching member, the rendered constraint row should include the person's display name (or full name), the severity label, the rule type label, and the formatted payload summary.

**Validates: Requirements 1.2**

---

### Property 2: Role constraint row contains required display fields

*For any* role constraint and any role list that contains a matching role, the rendered constraint row should include the role name, the severity label, the rule type label, and the formatted payload summary.

**Validates: Requirements 1.3**

---

### Property 3: Person name resolution uses displayName with fullName fallback

*For any* member list and any constraint whose `scopeId` matches a member in that list, the resolved display name should equal that member's `displayName` if non-null, otherwise `fullName`.

**Validates: Requirements 1.6**

---

### Property 4: Person selector contains only registered members

*For any* member list, the person selector in the personal constraint create form should contain exactly the members whose `invitationStatus` is `"accepted"`, and no others.

**Validates: Requirements 2.3, 8.1**

---

### Property 5: Role selector contains only active roles

*For any* role list, the role selector in the role constraint create form should contain exactly the roles where `isActive` is `true`, and no others.

**Validates: Requirements 3.3**

---

### Property 6: Edit form is pre-populated with constraint's current values

*For any* constraint (personal or role), opening the edit form should pre-populate the severity, payload, effectiveFrom, and effectiveUntil fields with that constraint's current values.

**Validates: Requirements 4.2**

---

### Property 7: Unregistered person is rejected with HTTP 422

*For any* person whose `linked_user_id` is null or whose `invitation_status` is not `"accepted"`, a `POST /spaces/{spaceId}/constraints` request with `scope_type = "person"` and that person's ID as `scope_id` should return HTTP 422 with the message "Personal constraints can only be applied to registered members."

**Validates: Requirements 8.2, 8.4**

---

### Property 8: Non-existent or inactive role is rejected with HTTP 404

*For any* GUID that does not correspond to an active `SpaceRole` in the given space, a `POST /spaces/{spaceId}/constraints` request with `scope_type = "role"` and that GUID as `scope_id` should return HTTP 404 with the message "Role not found in this space."

**Validates: Requirements 3.7, 6.3, 6.4**

---

### Property 9: Non-existent person is rejected with HTTP 404

*For any* GUID that does not correspond to a `Person` in the given space, a `POST /spaces/{spaceId}/constraints` request with `scope_type = "person"` and that GUID as `scope_id` should return HTTP 404 with the message "Person not found in this space."

**Validates: Requirements 2.7, 6.1, 6.2**

---

### Property 10: Active constraints within the solver horizon are included in the payload

*For any* set of active personal or role constraints, the solver payload builder should include exactly those constraints whose effective window overlaps the solver horizon start date, and exclude those whose `effective_until` is before the horizon start or whose `effective_from` is after the horizon end.

**Validates: Requirements 7.1, 7.2, 7.3**

---

## Error Handling

### Backend

All exceptions propagate to `ExceptionHandlingMiddleware` per the architecture rules:

| Exception type | HTTP status |
|---|---|
| `ArgumentException` (null/empty scopeId) | 400 |
| `KeyNotFoundException` (person/role not found) | 404 |
| `DomainValidationException` (unregistered person) | 422 |
| `UnauthorizedAccessException` (missing permission) | 403 |

`DomainValidationException` must be mapped to 422 in `ExceptionHandlingMiddleware`. Verify this mapping exists; if not, add it alongside the existing 400/404/403 mappings.

### Frontend

- **Create form errors** — displayed as a `<p className="text-sm text-red-600">` below the form submit button, inside `SectionCreateForm`. The error is cleared when the form is closed or re-submitted.
- **Edit form errors** — displayed in the existing `editConstraintError` slot in the edit modal.
- **Delete errors** — displayed per-row in `constraintDeleteErrors[id]`, already implemented in `ConstraintRow`.
- **API error message extraction** — the frontend should attempt to read `error.response?.data?.message` (Axios) and fall back to a generic Hebrew string if absent.

---

## Testing Strategy

### Unit tests (example-based)

**Backend** (`Jobuler.Application.Tests` or equivalent xUnit project):

- `CreateConstraintCommandHandler` — person scope with null `linked_user_id` → throws `DomainValidationException`
- `CreateConstraintCommandHandler` — person scope with `invitation_status = "pending"` → throws `DomainValidationException`
- `CreateConstraintCommandHandler` — person scope with non-existent person → throws `KeyNotFoundException`
- `CreateConstraintCommandHandler` — role scope with inactive role → throws `KeyNotFoundException`
- `CreateConstraintCommandHandler` — role scope with non-existent role → throws `KeyNotFoundException`
- `CreateConstraintCommandHandler` — group scope → succeeds without person/role checks
- `ExceptionHandlingMiddleware` — `DomainValidationException` → HTTP 422

**Frontend** (Jest + React Testing Library):

- `ConstraintsTab` renders three section headings when given a mixed constraint list
- `ConstraintRow` with no matching member shows `scopeId` as fallback label (Requirement 1.4)
- `SectionCreateForm` (person) — submit calls `onSubmit` with `scopeType = "person"` and selected `personId`
- `SectionCreateForm` (role) — submit calls `onSubmit` with `scopeType = "role"` and selected `roleId`
- Delete button triggers confirmation before calling `onDeleteConstraint`
- Edit modal does not render scope_type / scope_id / rule_type as editable fields

### Property-based tests

Property-based testing is applicable here because the feature has pure filtering and validation logic with large input spaces. The recommended library is **FsCheck** (C# / xUnit) for backend properties and **fast-check** (TypeScript / Jest) for frontend properties.

Each property test must run a minimum of **100 iterations**.

Tag format: `// Feature: personal-and-role-constraints, Property {N}: {property_text}`

**Backend property tests** (FsCheck + xUnit):

- **Property 7** — Generate random `Person` records with `linked_user_id = null` or `invitation_status = "pending"`. For each, invoke `CreateConstraintCommandHandler` with `scope_type = person`. Assert `DomainValidationException` is thrown every time.
  `// Feature: personal-and-role-constraints, Property 7: unregistered person rejected with 422`

- **Property 8** — Generate random GUIDs not present in `space_roles` (or present but `is_active = false`). For each, invoke handler with `scope_type = role`. Assert `KeyNotFoundException` is thrown every time.
  `// Feature: personal-and-role-constraints, Property 8: non-existent or inactive role rejected with 404`

- **Property 9** — Generate random GUIDs not present in `people` for the given space. For each, invoke handler with `scope_type = person`. Assert `KeyNotFoundException` is thrown every time.
  `// Feature: personal-and-role-constraints, Property 9: non-existent person rejected with 404`

- **Property 10** — Generate random lists of `ConstraintRule` records with varying `effective_from` / `effective_until` and a random solver horizon date. Assert the payload builder includes exactly the constraints whose window overlaps the horizon.
  `// Feature: personal-and-role-constraints, Property 10: active constraints within horizon included in payload`

**Frontend property tests** (fast-check + Jest):

- **Property 1** — Generate random `ConstraintDto` with `scopeType = "person"` and a matching `GroupMemberDto`. Render `ConstraintRow`. Assert the rendered output contains the member's name, severity label, rule type label, and payload summary.
  `// Feature: personal-and-role-constraints, Property 1: personal constraint row contains required display fields`

- **Property 2** — Generate random `ConstraintDto` with `scopeType = "role"` and a matching `GroupRoleDto`. Render `ConstraintRow`. Assert the rendered output contains the role name, severity label, rule type label, and payload summary.
  `// Feature: personal-and-role-constraints, Property 2: role constraint row contains required display fields`

- **Property 3** — Generate random `GroupMemberDto[]` and a random `scopeId` that matches one member. Assert the resolved name equals `displayName ?? fullName`.
  `// Feature: personal-and-role-constraints, Property 3: person name resolution uses displayName with fullName fallback`

- **Property 4** — Generate random `GroupMemberDto[]` with a mix of `invitationStatus` values. Render `SectionCreateForm` with `scopeType = "person"`. Assert the person selector options contain exactly the members with `invitationStatus === "accepted"`.
  `// Feature: personal-and-role-constraints, Property 4: person selector contains only registered members`

- **Property 5** — Generate random `GroupRoleDto[]` with a mix of `isActive` values. Render `SectionCreateForm` with `scopeType = "role"`. Assert the role selector options contain exactly the roles with `isActive === true`.
  `// Feature: personal-and-role-constraints, Property 5: role selector contains only active roles`

- **Property 6** — Generate random `ConstraintDto`. Simulate clicking the edit button. Assert the edit form fields are pre-populated with the constraint's `severity`, `rulePayloadJson`, `effectiveFrom`, and `effectiveUntil`.
  `// Feature: personal-and-role-constraints, Property 6: edit form pre-populated with constraint's current values`
