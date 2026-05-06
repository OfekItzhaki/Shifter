# Step 114 — Presence Windows API, Error Handling, and Validation

## Phase
Phase 4 — Quality & Correctness

## Purpose
Implements several improvements identified during the project scan:
1. **Presence windows API** — the member availability UI was calling endpoints that didn't exist. Added GET, POST, DELETE endpoints.
2. **Error handling** — `schedule/today`, `schedule/tomorrow`, and `admin/logs` silently swallowed errors. Now show error messages.
3. **Dead code removal** — removed unused cookie-setting code from `apiClient.ts`.
4. **Stale DTO** — `useGroups.ts` was missing `solverStartDateTime` field.
5. **Input validation** — added FluentValidation for `CreateGroupCommand` and `UpdateGroupSettingsCommand`.

## What was built

### Backend
- **`apps/api/Jobuler.Application/People/Commands/DeletePresenceWindowCommand.cs`** — new command to delete a manually-created presence window. Guards against deleting derived (auto-generated) windows.
- **`apps/api/Jobuler.Api/Controllers/PeopleController.cs`** — added three endpoints:
  - `GET /spaces/{spaceId}/people/{personId}/presence` — lists non-derived presence windows
  - `POST /spaces/{spaceId}/people/{personId}/presence` — creates a manual unavailability window
  - `DELETE /spaces/{spaceId}/people/{personId}/presence/{windowId}` — removes a window
  - All require `PeopleManage` permission.
- **`apps/api/Jobuler.Application/Groups/Validators/GroupCommandValidators.cs`** — added `CreateGroupCommandValidator` (name required, max 100 chars) and `UpdateGroupSettingsCommandValidator` (horizon 1–90 days).

### Frontend
- **`apps/web/app/schedule/today/page.tsx`** — added `error` state; shows a red error banner if the schedule fails to load.
- **`apps/web/app/schedule/tomorrow/page.tsx`** — same fix.
- **`apps/web/app/admin/logs/page.tsx`** — added `error` state; shows error instead of silently showing empty table.
- **`apps/web/lib/api/client.ts`** — removed dead `document.cookie` line that set a cookie never read by the app.
- **`apps/web/lib/query/hooks/useGroups.ts`** — added `solverStartDateTime?: string | null` to `GroupDto` to match the API response.
- **`apps/web/app/groups/[groupId]/tabs/MembersTab.tsx`** — added delete button (✕) to each presence window row so admins can remove unavailability windows.

## Key decisions
- The `GetPresenceQuery` already existed in `GetAvailabilityQuery.cs` — reused it rather than creating a duplicate.
- Only non-derived presence windows are returned by the GET endpoint and deletable — derived windows (auto-generated from assignments) are read-only.
- The `AddPresenceWindowCommand` already existed and was complete — only the controller endpoints were missing.

## How to run / verify
```bash
dotnet test apps/api/Jobuler.Tests/Jobuler.Tests.csproj
# Expected: 364 passed, 0 failed
```

To test presence windows manually:
1. Open a group → Members tab → click "Details" on any member
2. Switch to the "Availability" tab
3. Add an unavailability window — it should save and appear in the list
4. Click ✕ to remove it

## What comes next
- Add aria-labels to icon buttons for accessibility
- Add `SolverStartDateTime` validation (must be in future or null)

## Git commit
```bash
git add -A && git commit -m "feat(people): presence windows API endpoints, error handling, dead code removal, validation"
```
