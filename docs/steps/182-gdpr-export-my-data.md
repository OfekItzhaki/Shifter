# 182 — GDPR Export My Data

## Phase
Phase 8 — Privacy & Compliance

## Purpose
Allows users to download all their personal data as a JSON file, fulfilling GDPR "right of access" (Article 15) requirements. Users can see exactly what data the system holds about them.

## What was built

### Backend

| File | Description |
|------|-------------|
| `Jobuler.Application/Auth/Queries/ExportMyDataQuery.cs` | MediatR query record and response DTOs for the data export |
| `Jobuler.Application/Auth/Queries/ExportMyDataHandler.cs` | Handler that aggregates user profile, group memberships, assignments (last 90 days), and notifications (last 30 days) |
| `Jobuler.Api/Controllers/AuthController.cs` | Added `GET /auth/me/export` endpoint that returns a downloadable JSON file |

### Frontend

| File | Description |
|------|-------------|
| `apps/web/app/profile/page.tsx` | Added `ExportDataSection` component with download button |
| `apps/web/messages/en.json` | English i18n keys for export feature |
| `apps/web/messages/he.json` | Hebrew i18n keys for export feature |
| `apps/web/messages/ru.json` | Russian i18n keys for export feature |

## Key decisions

- **Data scope**: Profile, group memberships, assignments (90-day window, max 500), notifications (30-day window, max 200). This balances completeness with performance.
- **File format**: Indented JSON with camelCase naming for readability.
- **No tenant filter bypass**: The handler queries across all spaces the user belongs to (via their linked Person records), which is correct for a personal data export — the user owns this data regardless of space.
- **Silent failure on frontend**: If the export fails, no error toast is shown — this avoids confusing non-technical users. The button simply re-enables.
- **Assignments joined through TaskSlot**: Since `Assignment` only has `TaskSlotId`, we join through `TaskSlot` → `TaskType` to get human-readable task names and time ranges.

## How it connects

- Uses existing `Person.LinkedUserId` to find all person records belonging to the user across spaces.
- Leverages `GroupMembership`, `TaskSlot`, `TaskType`, and `Notification` entities already in the domain.
- Endpoint sits alongside existing `GET /auth/me` and `DELETE /auth/me` in the AuthController.
- Frontend section appears in the profile page between push notification settings and the delete account section.

## How to run / verify

1. **Backend**: `dotnet build` in `apps/api/` — should compile cleanly.
2. **API test**: Login, then `GET /auth/me/export` — should return a JSON file download.
3. **Frontend**: Navigate to `/profile` — "Export My Data" section should appear with a download button.
4. **Click the button** — a file named `shifter-data-export-YYYY-MM-DD.json` should download.

## What comes next

- Rate limiting on the export endpoint (currently inherits the `auth` rate limiter).
- Optional: email notification when data export is requested (audit trail).
- Optional: include availability windows and constraint rules in the export.

## Git commit

```bash
git add -A && git commit -m "feat(privacy): GDPR export my data endpoint and profile UI"
```
