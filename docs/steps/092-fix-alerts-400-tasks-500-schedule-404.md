# Step 092 — Fix: Alerts 400, Tasks 500, Schedule 404

## Phase
Phase 9 — Bug Fixes

## Purpose
Three issues reported after the API restart:

1. **`POST /alerts → 400`** — Frontend was sending `severity: "success"` which the backend validator rejects (only `info`, `warning`, `critical` are valid).
2. **`POST /tasks → 500`** — DB check constraint violations (e.g. `ends_at > starts_at`, `daily_window_both_or_neither`) were falling through to the generic 500 handler instead of returning a meaningful 400. Also, the frontend was sending datetime strings without timezone info, which could cause `ends_at <= starts_at` after server-side parsing.
3. **`GET /schedule-versions/current → 404`** — Not a bug. The API correctly returns 404 when no schedule has been published. The frontend already handles it gracefully (empty schedule table). Console noise only.

## What was built

### Frontend — `AlertsTab.tsx`
Removed `"success"` from the `SEVERITIES` array. The backend `AlertSeverity` enum only has `Info`, `Warning`, `Critical`. The "הצלחה" option was causing every alert creation to fail with 400.

### Backend — `ExceptionHandlingMiddleware.cs`
Added a new case to the exception switch for PostgreSQL check constraint violations (error code `23514` / message contains `"violates check constraint"`). These now return **400 Bad Request** with a user-friendly message instead of 500. Added `ExtractCheckConstraintMessage` helper that maps known constraint names to readable messages:
- `chk_task_ends_after_starts` → "End time must be after start time."
- `chk_task_shift_duration` → "Shift duration must be at least 1 minute."
- `chk_task_headcount_positive` → "Required headcount must be at least 1."
- `chk_task_daily_window_both_or_neither` → "Daily start time and end time must both be set, or both left empty."
- `chk_slot_order` → "Slot end time must be after start time."

### Frontend — `page.tsx` (task submit handler)
- Datetime strings now converted to full ISO 8601 with `new Date(...).toISOString()` before sending — ensures timezone is included and the server parses them correctly.
- Added client-side guard: if `endsAt <= startsAt`, shows Hebrew error "תאריך הסיום חייב להיות אחרי תאריך ההתחלה" before even hitting the API.
- Added client-side guard: if only one of `dailyStartTime`/`dailyEndTime` is set, shows "יש להגדיר שעת התחלה וסיום יומית יחד, או להשאיר שניהם ריקים".
- `shiftDurationMinutes` clamped to `Math.max(1, ...)` to prevent sending 0.

## Key decisions
- **Client-side validation first**: catches the most common mistakes before a round-trip to the server.
- **Backend still validates**: the middleware improvement means even if the frontend validation is bypassed, the user gets a 400 with a clear message instead of a cryptic 500.
- **Schedule 404 is not a bug**: no code change needed. The page already shows an empty schedule table when no schedule exists.

## How to verify
1. Restart the API (stop + `dotnet run`).
2. Create an alert → should succeed with any of the three severity options.
3. Create a task with `endsAt` before `startsAt` → frontend shows Hebrew error immediately, no API call made.
4. Create a valid task → should succeed (201).
5. View the schedule tab with no published schedule → empty table, no error shown to user.

## Git commit
```bash
git add -A && git commit -m "fix(alerts): remove invalid success severity; fix(tasks): proper datetime ISO + client validation; fix(middleware): check constraint violations return 400"
```
