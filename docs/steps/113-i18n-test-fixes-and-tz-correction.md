# Step 113 — i18n Fixes, Test Fixes, and Timezone Correction

## Phase
Phase 4 — Quality & Correctness

## Purpose
Fixes several issues identified during a post-implementation review:
1. `en.json` had `settings_tab` keys floating outside the `groups` object (invalid JSON structure)
2. `ru.json` was missing `solverStartFrom` and `solverStartFromHint` keys
3. `GroupsController.ConfirmTransfer` returned a hardcoded Hebrew message
4. `AdminManagementIntegrationTests.MakePublishHandler` was missing the `scopeFactory` argument (build failure)
5. `AutoSchedulerBugConditionTests` was testing the old buggy behavior instead of the fix
6. `SolverStartDateTime` UI was converting UTC→ISO instead of UTC→local time for the `datetime-local` input

## What was built

- **`apps/web/messages/en.json`** — Wrapped the floating settings_tab keys inside a proper `"settings_tab": { ... }` object, matching the structure in `he.json` and `ru.json`.
- **`apps/web/messages/ru.json`** — Added `solverStartFrom` and `solverStartFromHint` keys to the `settings_tab` section.
- **`apps/api/Jobuler.Api/Controllers/GroupsController.cs`** — Replaced hardcoded Hebrew `"הבעלות הועברה בהצלחה"` with neutral English `"Ownership transferred successfully."` in `ConfirmTransfer`.
- **`apps/api/Jobuler.Tests/Integration/AdminManagementIntegrationTests.cs`** — Added missing `scopeFactory` argument to `MakePublishHandler` to match the updated `PublishVersionCommandHandler` constructor.
- **`apps/api/Jobuler.Tests/Scheduling/AutoSchedulerBugConditionTests.cs`** — Rewrote tests to validate the fix (not the bug). Now tests: `UpdateSettings` stores `SolverStartDateTime`, null clears it, `TriggerSolverCommand` carries `GroupId` + `StartTime`, null `SolverStartDateTime` produces null `StartTime`.
- **`apps/web/app/groups/[groupId]/page.tsx`** — Fixed `SolverStartDateTime` loading: converts UTC API value to local time using `Date` methods (not `.toISOString().slice(0,16)` which gives UTC) so the `datetime-local` input shows the correct local time.

## Key decisions
- `datetime-local` inputs always work in the browser's local timezone — the value must be formatted as local time, not UTC. We use `d.getFullYear()`, `d.getMonth()`, etc. (local methods) instead of `d.toISOString()` (UTC).
- The `ConfirmTransfer` endpoint is `[AllowAnonymous]` and called from an email link — the frontend handles the success message via the `transfer.successMessage` i18n key, so the API response message doesn't need to be translated.

## How to run / verify
```bash
dotnet test apps/api/Jobuler.Tests/Jobuler.Tests.csproj
# Expected: 364 passed, 0 failed
```

## What comes next
- Broader project scan for additional improvements

## Git commit
```bash
git add -A && git commit -m "fix(i18n): fix en.json settings_tab structure, add ru translations, fix tz conversion and test failures"
```
