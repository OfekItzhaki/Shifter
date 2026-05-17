# 312 — Platform Settings API Endpoints

## Phase

Admin Session Timeout — API Controller Endpoints

## Purpose

Expose `GET /platform/settings` and `PATCH /platform/settings` endpoints so the super-admin can read and update platform-level configuration (currently the platform session timeout duration). These endpoints are required by Requirements 4.3 and 4.5.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/PlatformController.cs` | Added `GET /platform/settings` endpoint that returns current platform settings (platformTimeoutMinutes). Added `PATCH /platform/settings` endpoint that accepts `{ platformTimeoutMinutes: int }` and dispatches `UpdatePlatformSettingsCommand`. Both endpoints enforce platform admin check. Added `PlatformSettingsResponse` and `UpdatePlatformSettingsRequest` record DTOs. |

## Key decisions

- **Platform admin check inline**: Follows the existing pattern in `PlatformController` where each endpoint loads the user and checks `IsPlatformAdmin` directly, rather than using a policy attribute. This is consistent with all other platform endpoints.
- **PATCH returns 204 NoContent**: The update endpoint returns no body on success, following REST conventions for PATCH operations.
- **GET returns default 15 if setting not found**: Defensive fallback in case the seed row is missing, matching the domain default.
- **DTOs outside the controller class**: `PlatformSettingsResponse` and `UpdatePlatformSettingsRequest` are defined as top-level records in the same file for simplicity, following the pattern used in other controllers.

## How it connects

- `PATCH /platform/settings` dispatches `UpdatePlatformSettingsCommand` (task 3.3) which validates the range [5, 120] and persists the value.
- `GET /platform/settings` reads directly from `PlatformSettings` entity (task 1.4) via `AppDbContext`.
- The frontend platform settings UI (task 10.2) will call these endpoints.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

Build should succeed with no errors or warnings.

## What comes next

- Task 4.5: Property test for progressive delay enforcement
- Task 10.2: Frontend platform timeout settings UI that calls these endpoints

## Git commit

```bash
git add -A && git commit -m "feat(admin-session-timeout): add GET/PATCH /platform/settings endpoints"
```
