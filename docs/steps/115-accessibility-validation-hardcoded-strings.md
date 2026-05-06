# Step 115 — Accessibility, Validation, and Hardcoded String Fixes

## Phase
Phase 4 — Quality & Correctness

## Purpose
Continued improvements from the project scan:
1. Added `aria-label` to group card buttons for screen reader accessibility
2. Added past-date warning to the `SolverStartDateTime` input
3. Replaced hardcoded Hebrew notification message in `PublishVersionCommand` with English
4. Confirmed no N+1 queries in the publish notification loop (false positive from scan)

## What was built

- **`apps/web/app/groups/page.tsx`** — Added `aria-label` to each group card button describing the group name and member count.
- **`apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx`** — Added a warning message when the configured `SolverStartDateTime` is in the past, so admins know the solver will start from a historical point.
- **`apps/api/Jobuler.Application/Scheduling/Commands/PublishVersionCommand.cs`** — Replaced hardcoded Hebrew in-app notification strings with English. The external notification messages (WhatsApp/email) remain locale-aware via the `SolverWorkerService` locale switch.

## Key decisions
- The `SolverStartDateTime` past-date warning is advisory only (amber, not blocking) — there are valid use cases for scheduling from a past date (e.g. backfilling).
- The `SolverWorkerService` locale switch (Hebrew default) is intentional and correct — it uses the space's configured locale to send user-facing messages.
- The publish notification loop was confirmed to be N+1-free: it loads all members and users in two bulk queries before iterating.

## How to run / verify
```bash
dotnet test apps/api/Jobuler.Tests/Jobuler.Tests.csproj
# Expected: 364 passed, 0 failed
```

## What comes next
- Add `TriggerMode` enum validation to `TriggerSolverRequest`
- Extract magic numbers to constants

## Git commit
```bash
git add -A && git commit -m "fix(ux): aria-labels, past-date warning, hardcoded Hebrew notification strings"
```
