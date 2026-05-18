# Step 357 — UserSettingsController API Endpoints

## Phase

User Timezone Settings — Backend API Layer

## Purpose

Expose the user settings functionality via REST endpoints so the frontend can read and update a user's geographic location (Country/State) and retrieve their resolved timezone.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/UserSettingsController.cs` | New controller with `PUT /api/user-settings/location` and `GET /api/user-settings` endpoints |

## Key decisions

- **Route prefix `api/user-settings`** — Follows the pattern of user-scoped endpoints that don't require a space context in the URL (similar to `/feedback`).
- **UserId from JWT claims** — The controller extracts the current user's ID from the JWT `NameIdentifier` claim, so users can only update their own settings. No additional permission check is needed.
- **No business logic in controller** — The controller only dispatches MediatR commands/queries. Validation happens in the Application layer via FluentValidation.
- **`[Authorize]` at class level** — All endpoints require authentication per security rules.
- **Tenant context validated via middleware** — `TenantContextMiddleware` is already in the pipeline and runs before controller logic.

## How it connects

- Dispatches `UpdateUserLocationCommand` (implemented in step 356) which persists location and resolves timezone.
- Dispatches `GetUserSettingsQuery` (implemented in step 356) which reads user settings and resolves current timezone.
- The frontend Settings page (task 8.2) will call these endpoints.
- The `PUT` response returns the resolved timezone so the frontend can update the auth store immediately without re-login.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
# Endpoints available at:
#   PUT  /api/user-settings/location  (body: { "countryCode": "US", "stateCode": "CA" })
#   GET  /api/user-settings
# Both require a valid JWT Bearer token.
```

## What comes next

- Task 4.2: Unit tests for UserSettingsController (validate HTTP status codes and error handling)
- Task 6.1: Frontend auth store integration to consume the timezone fields from login/settings responses

## Git commit

```bash
git add -A && git commit -m "feat(timezone): add UserSettingsController with location endpoints"
```
