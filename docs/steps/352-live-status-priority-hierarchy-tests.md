# 352 — Live Status Priority Hierarchy Tests

## Phase

Home Leave Protection — Task 8.1

## Purpose

Verify and fix the live status priority hierarchy in `GetGroupLiveStatusQuery`. The query must evaluate presence windows before assignment-based status, ensuring AtHome windows always take precedence over OnMission derived from assignments.

## What was built

- **`apps/api/Jobuler.Application/Scheduling/Queries/GetGroupLiveStatusQuery.cs`** — Fixed the status string mapping. The original code used `pw.State.ToString().ToLower()` which produces `"athome"` (no underscore), but the switch cases expected `"at_home"`. Changed to match directly on the `PresenceState` enum values, which is both correct and more efficient.

- **`apps/api/Jobuler.Tests/Scheduling/GetGroupLiveStatusPriorityTests.cs`** — Added 6 explicit unit tests covering all priority combinations:
  1. No presence window, no assignment → `"free_in_base"`
  2. Active assignment, no presence window → `"on_mission"`
  3. AtHome window, no assignment → `"at_home"`
  4. AtHome window + active assignment → `"at_home"` (precedence)
  5. Multiple members with all combinations simultaneously
  6. Derived AtHome window + assignment → `"at_home"` (precedence)

## Key decisions

- **Enum-based switch instead of string-based**: The original code converted the enum to a lowercase string and matched against snake_case strings. This was a latent bug — `PresenceState.AtHome.ToString().ToLower()` = `"athome"`, not `"at_home"`. Switching to pattern match on the enum directly is type-safe and eliminates the string conversion issue.

- **Removed "blocked" case**: The `PresenceState` enum only has `FreeInBase`, `AtHome`, and `OnMission`. There is no `Blocked` state, so the dead `"blocked"` case was removed.

## How it connects

- Validates Requirements 6.1–6.5 from the home-leave-protection spec
- The priority hierarchy ensures the live status panel always shows the correct physical location
- This fix ensures that people on home leave are correctly shown as "at_home" in the live status view, even if they have stale assignments

## How to run / verify

```bash
cd apps/api/Jobuler.Tests
dotnet test --filter "FullyQualifiedName~GetGroupLiveStatusPriorityTests"
```

All 6 tests should pass.

## What comes next

- Task 8.2: Property-based test for live status priority hierarchy (FsCheck)
- Task 9: Wire recall notification dispatch into CancelHomeLeaveCommand handler

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-protection): fix live status priority hierarchy and add unit tests"
```
