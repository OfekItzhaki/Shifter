# Design Document: Color-Coded Roles

## Overview

This feature adds an optional color property to the SpaceRole entity, allowing admins to assign a preset color to roles. The color is displayed as a left-border/dot indicator next to person names in schedule views and member lists, providing visual team/unit separation.

## Architecture

The feature follows the existing 4-layer architecture:

```
Domain  → SpaceRole gains a Color property
Application → Commands/Queries updated to include color; validation added
Infrastructure → EF Core config + migration for the color column
Api → Controller request DTOs accept color field
Web → Color palette in role form; color indicator in schedule/member views
```

## Components

### 1. Domain Layer — SpaceRole Entity

Add an optional `Color` property to `SpaceRole`:

```csharp
// In SpaceRole.cs
public string? Color { get; private set; }
```

Update factory methods and `Update` to accept color:

```csharp
public static SpaceRole CreateForGroup(
    Guid spaceId, Guid groupId, string name, Guid createdByUserId,
    string? description = null,
    RolePermissionLevel permissionLevel = RolePermissionLevel.View,
    bool isDefault = false,
    string? color = null) =>
    new()
    {
        SpaceId = spaceId,
        GroupId = groupId,
        Name = name.Trim(),
        Description = description?.Trim(),
        CreatedByUserId = createdByUserId,
        PermissionLevel = permissionLevel,
        IsDefault = isDefault,
        Color = color
    };

public void Update(string name, string? description, RolePermissionLevel? permissionLevel = null, string? color = null)
{
    Name = name.Trim();
    Description = description?.Trim();
    if (permissionLevel.HasValue) PermissionLevel = permissionLevel.Value;
    Color = color;
    Touch();
}
```

### 2. Infrastructure Layer — EF Core Configuration & Migration

**SpaceRoleConfiguration** — add color column mapping:

```csharp
builder.Property(r => r.Color).HasColumnName("color").HasMaxLength(7).IsRequired(false);
```

**Migration 044_space_role_color.sql**:

```sql
-- Add optional color column to space_roles for visual role identification
ALTER TABLE space_roles
    ADD COLUMN IF NOT EXISTS color TEXT DEFAULT NULL
    CHECK (color IS NULL OR color ~ '^#[0-9a-fA-F]{6}$');
```

### 3. Application Layer — Commands & Queries

**CreateGroupRoleCommand** — add `Color` parameter:

```csharp
public record CreateGroupRoleCommand(
    Guid SpaceId, Guid GroupId, string Name, string? Description,
    string PermissionLevel, Guid CurrentUserId, string? Color = null) : IRequest<Guid>;
```

**UpdateGroupRoleCommand** — add `Color` parameter:

```csharp
public record UpdateGroupRoleCommand(
    Guid SpaceId, Guid GroupId, Guid RoleId, string Name, string? Description,
    string PermissionLevel, Guid CurrentUserId, string? Color = null) : IRequest;
```

**Validation** — FluentValidation rule for color:

```csharp
RuleFor(x => x.Color)
    .Matches(@"^#[0-9a-fA-F]{6}$")
    .When(x => x.Color != null)
    .WithMessage("Color must be a valid hex color (e.g., '#f59e0b')");
```

**GetGroupRolesQuery response** — include color in the DTO returned to the API.

### 4. API Layer — Controller & Request DTO

Update `GroupRoleRequest` to include color:

```csharp
public record GroupRoleRequest(string Name, string? Description, string? PermissionLevel, string? Color);
```

Pass color through to commands in `GroupRolesController.Create` and `GroupRolesController.Update`.

### 5. Frontend — API Client DTO

Update `GroupRoleDto` in `lib/api/groups.ts`:

```typescript
export interface GroupRoleDto {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  isDefault: boolean;
  permissionLevel: "View" | "ViewAndEdit" | "Owner";
  color: string | null;  // hex color e.g. "#f59e0b"
}
```

Update `createGroupRole` and `updateGroupRole` payloads to include `color`.

### 6. Frontend — Color Palette Component

A simple `RoleColorPicker` component with 8-10 preset colors:

```typescript
const ROLE_COLOR_PALETTE = [
  "#ef4444", // red
  "#f97316", // orange
  "#f59e0b", // amber
  "#22c55e", // green
  "#06b6d4", // cyan
  "#3b82f6", // blue
  "#8b5cf6", // violet
  "#ec4899", // pink
  "#6b7280", // gray (explicit neutral)
] as const;
```

The picker renders small colored circles. Clicking one selects it; clicking the selected one deselects (sets to null). The currently selected color has a ring/check indicator.

### 7. Frontend — Color Indicator in Schedule Views

A utility function determines the indicator style:

```typescript
function getRoleColorStyle(roleColor: string | null): React.CSSProperties | undefined {
  if (!roleColor) return undefined;
  return { borderLeftColor: roleColor, borderLeftWidth: '3px', borderLeftStyle: 'solid' };
}
```

Applied to the person name cell/div in:
- `ScheduleTaskTable.tsx` — on the person name `<span>` wrapper
- `ScheduleTable2D.tsx` — on the person name `<div>` wrapper

For the member list, a small colored dot is rendered before the role badge text.

### 8. Data Flow

1. Admin opens role form → palette shown (pre-selected if editing)
2. Admin picks a color → local state updated
3. Admin saves → API call includes `color` field
4. Backend validates hex format → persists to `space_roles.color`
5. Schedule/member views fetch roles → `GroupRoleDto.color` available
6. Person name rendering looks up their role's color → applies indicator style

### 9. Passing Role Color to Schedule Views

The schedule views currently receive `TaskAssignment[]` which includes `personName` but not role info. To display role colors:

- The group page already fetches `groupRoles` and `members` (with `roleId`).
- Build a lookup map: `personId → roleColor` by joining members' `roleId` to `groupRoles`.
- Pass this map (or a helper function) to the schedule table components as a new prop.
- The schedule components use the person's ID (already available as `personId` in `TaskAssignment`) to look up their color.

## Error Handling

- Invalid hex color in API request → 400 Bad Request via FluentValidation
- Database CHECK constraint as a safety net for invalid values
- Frontend palette only offers valid colors, so invalid input is unlikely from the UI

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Hex color validation correctness

*For any* string input, the color validation function SHALL accept the input if and only if the input is null or matches the regex `^#[0-9a-fA-F]{6}$`.

**Validates: Requirements 1.5, 4.3, 4.4**

### Property 2: Role color round-trip persistence

*For any* valid color from the preset palette, creating or updating a role with that color and then reading the role back SHALL return the same color value unchanged.

**Validates: Requirements 1.3, 4.2**

### Property 3: Color indicator rendering consistency

*For any* person with a role that has a non-null color, the rendered schedule cell SHALL contain a color indicator element styled with that exact color value. *For any* person with a null role color or no role, the rendered cell SHALL not contain a color indicator element.

**Validates: Requirements 2.1, 2.2, 2.3, 3.1, 3.2**
