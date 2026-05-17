# Step 313 — Group Settings GET Response: ManagementTimeoutMinutes

## Phase

Admin Session Timeout — API Controller Endpoints

## Purpose

Expose the `managementTimeoutMinutes` field in the GET groups response so the frontend can read the configured timeout duration for each group. The PATCH request model already accepted this field (added in step 309), but the GET response did not return it.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/Groups/Queries/GetGroupsQuery.cs` | Added `ManagementTimeoutMinutes` parameter (default 15) to `GroupDto` record and included `g.ManagementTimeoutMinutes` in the query handler's Select projection |

## Key decisions

- Used a default value of `15` in the `GroupDto` record to match the domain entity default and database column default, ensuring backward compatibility with any code that constructs `GroupDto` without this parameter.
- No separate "group settings GET" endpoint was needed — the existing `GET /spaces/{spaceId}/groups` already returns all group data including settings fields.

## How it connects

- **Requirement 8.1**: The PATCH endpoint (already wired in step 309) accepts `managementTimeoutMinutes` in the request body.
- **Requirement 8.2**: This step ensures the GET response includes the current `managementTimeoutMinutes` value.
- The frontend timeout configuration UI (task 10.1) will read this value to display the current setting.

## How to run / verify

```bash
cd apps/api
dotnet build
dotnet test --filter "FullyQualifiedName~Groups"
```

All 18 group tests pass. The `GroupDto` now includes `managementTimeoutMinutes` in the JSON response.

## What comes next

- Task 4.4: Platform settings GET/PATCH endpoints
- Task 10.1: Frontend timeout configuration UI that reads this value

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): include managementTimeoutMinutes in group GET response"
```
