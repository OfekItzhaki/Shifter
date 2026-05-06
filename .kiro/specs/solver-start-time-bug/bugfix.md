# Bugfix Requirements Document

## Introduction

The solver is generating draft schedules using the current system time (`DateTime.UtcNow`) or a UI-provided override as the horizon start, instead of using a configured start date and time stored in group settings. This causes the solver to generate schedules that begin from the wrong point in time, ignoring the mission's intended start date and time configured by the group owner.

The user has restarted the API, canceled the last draft, and re-ran the solver, but the issue persists because the root cause is architectural: the `Group` entity has no persisted `SolverStartDateTime` field, and the `SolverPayloadNormalizer` always defaults to `DateTime.UtcNow` when no `startTime` parameter is provided.

This fix will add a `SolverStartDateTime` field to the `Group` entity, expose it in the group settings UI, and ensure the solver uses this configured value when building the payload for auto-scheduled runs.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN the auto-scheduler (`AutoSchedulerService`) triggers a solver run for a group THEN the system passes `StartTime = null` in the `TriggerSolverCommand`, causing the solver to use `DateTime.UtcNow` as the horizon start instead of the group's configured start date and time.

1.2 WHEN the `SolverPayloadNormalizer.BuildAsync` method receives `startTime = null` THEN the system sets `nowUtc = DateTime.UtcNow` and uses this as the horizon start datetime, ignoring any group-level configured start date and time.

1.3 WHEN a group owner configures a mission start date and time in the group settings THEN the system does not persist this value in the `Group` entity, and the solver never uses it when generating schedules.

1.4 WHEN the solver expands `GroupTask` shifts into individual slots THEN the system uses `horizonStartDt = nowUtc` as the effective start boundary, which may be earlier or later than the mission's intended start date and time configured by the group owner.

### Expected Behavior (Correct)

2.1 WHEN the auto-scheduler triggers a solver run for a group THEN the system SHALL pass the group's configured `SolverStartDateTime` (if set) as the `StartTime` parameter in the `TriggerSolverCommand`, so the solver uses the group's configured start date and time as the horizon start.

2.2 WHEN the `SolverPayloadNormalizer.BuildAsync` method receives a `startTime` parameter THEN the system SHALL use this value as `nowUtc` and `horizonStartDt`, ensuring the solver generates schedules starting from the configured date and time.

2.3 WHEN a group owner configures a mission start date and time in the group settings UI THEN the system SHALL persist this value in the `Group.SolverStartDateTime` field and use it for all subsequent auto-scheduled solver runs.

2.4 WHEN the solver expands `GroupTask` shifts into individual slots THEN the system SHALL use `horizonStartDt = group.SolverStartDateTime` (if set) or `DateTime.UtcNow` (if not set) as the effective start boundary, ensuring shifts are generated from the correct starting point.

2.5 WHEN a group owner manually triggers the solver from the settings tab THEN the system SHALL use the datetime-local input value (which defaults to now) as the `StartTime` parameter, preserving the existing manual override behavior.

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a group has no `SolverStartDateTime` configured (null) THEN the system SHALL CONTINUE TO use `DateTime.UtcNow` as the horizon start, preserving backward compatibility for groups that have not set a custom start date and time.

3.2 WHEN a group owner manually triggers the solver from the settings tab with a custom start time THEN the system SHALL CONTINUE TO use the UI-provided `startTime` parameter, overriding the group's configured `SolverStartDateTime` for that specific run.

3.3 WHEN the solver filters availability windows, presence windows, and task slots by the horizon start datetime THEN the system SHALL CONTINUE TO use the same filtering logic (`EndsAt >= horizonStartDt AND StartsAt <= horizonEndDt`), ensuring no change in how the solver selects data within the horizon.

3.4 WHEN the `UpdateGroupSettingsCommand` updates the `SolverHorizonDays` field THEN the system SHALL CONTINUE TO save this value to the database and use it to calculate the horizon end date, ensuring the horizon length is preserved independently of the start date and time.
