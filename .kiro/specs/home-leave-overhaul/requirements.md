# Requirements Document

## Introduction

This feature overhauls the home-leave configuration system in Shifter, replacing the current abstract threshold/balance approach with a three-mode system: Automatic (smart slider with optimal ratio calculation), Manual Override (explicit day ratios with real-time feasibility feedback), and Emergency Freeze (immediate leave suspension with optional task scheduling). The overhaul eliminates the need for admins to understand hours or thresholds, replacing them with intuitive day-based controls. The system supports Hebrew, English, and Russian localization with correct RTL/LTR behavior.

## Glossary

- **Home_Leave_System**: The subsystem responsible for configuring, computing, and enforcing home-leave rotation parameters for closed-base groups
- **Automatic_Mode**: The default operating mode where the system calculates an optimal base:home ratio and presents a slider centered on that optimum
- **Manual_Mode**: An alternative operating mode where the admin explicitly sets the number of days at base and days at home
- **Emergency_Freeze**: A mode that immediately suspends all home-leave for the group until deactivated
- **Optimal_Ratio**: The calculated base:home day ratio that maximizes home time while maintaining minimum task coverage for the group
- **Feasibility_Engine**: The component that evaluates whether a given base:home configuration can satisfy task coverage requirements
- **Ratio_Slider**: The UI control in Automatic Mode that allows shifting the base:home ratio around the calculated optimum
- **Leave_Duration**: The number of hours each individual home visit lasts (separate from the ratio)
- **Coverage_Requirement**: The minimum number of people that must remain at base to cover all scheduled tasks at any given time
- **Cumulative_Tracker**: The existing subsystem that tracks consecutive_hours_at_base per person
- **Solver**: The CP-SAT constraint solver that generates schedules including home-leave assignments
- **Admin**: A user with permissions to manage home-leave configuration for a group
- **Leave_Queue**: The ordered list of personnel awaiting home-leave, determined by cumulative hours at base and priority

## Requirements

### Requirement 1: Mode Selection

**User Story:** As an Admin, I want to switch between Automatic, Manual, and Emergency Freeze modes, so that I can choose the level of control appropriate for the current situation.

#### Acceptance Criteria

1. THE Home_Leave_System SHALL support three mutually exclusive operating modes: Automatic_Mode, Manual_Mode, and Emergency_Freeze
2. WHEN the Admin selects a mode, THE Home_Leave_System SHALL persist the selected mode and apply it to all subsequent solver runs for the group
3. WHEN no mode has been explicitly selected, THE Home_Leave_System SHALL default to Automatic_Mode
4. WHEN the Admin switches from one mode to another, THE Home_Leave_System SHALL recalculate solver parameters according to the newly selected mode
5. THE Home_Leave_System SHALL display a clear toggle or segmented control allowing the Admin to switch between Automatic_Mode and Manual_Mode
6. THE Home_Leave_System SHALL display Emergency_Freeze as a separate prominent toggle independent of the Automatic/Manual selection

### Requirement 2: Optimal Ratio Calculation

**User Story:** As an Admin, I want the system to calculate the best possible base:home ratio for my group, so that I can maximize home time without compromising task coverage.

#### Acceptance Criteria

1. THE Home_Leave_System SHALL calculate the Optimal_Ratio based on: group member count, Leave_Duration, and Coverage_Requirement
2. THE Home_Leave_System SHALL express the Optimal_Ratio as whole-day values (e.g., "5:2" meaning 5 days at base, 2 days at home)
3. WHEN the group member count, Coverage_Requirement, or Leave_Duration changes, THE Home_Leave_System SHALL recalculate the Optimal_Ratio
4. THE Home_Leave_System SHALL compute the Optimal_Ratio using the formula: minimum base days = ceil((Coverage_Requirement × cycle_length) / (member_count - leave_capacity)), where cycle_length = base_days + Leave_Duration in days
5. IF the calculated Optimal_Ratio results in fewer than 1 day at home, THEN THE Home_Leave_System SHALL set the Optimal_Ratio to the minimum feasible configuration and indicate reduced home-leave availability
6. THE Home_Leave_System SHALL compute the Optimal_Ratio within 500 milliseconds for groups of up to 50 members

### Requirement 3: Automatic Mode Slider

**User Story:** As an Admin, I want a slider that shows me the actual day ratio and lets me adjust around the optimum, so that I can fine-tune the balance without understanding hours or thresholds.

#### Acceptance Criteria

1. WHILE Automatic_Mode is active, THE Ratio_Slider SHALL display with the Optimal_Ratio positioned at the center of the slider range
2. WHEN the Admin moves the Ratio_Slider to the right of center, THE Home_Leave_System SHALL increase base days relative to home days (more conservative — fewer people go home)
3. WHEN the Admin moves the Ratio_Slider to the left of center, THE Home_Leave_System SHALL decrease home days relative to base days (also conservative from the home side — shorter home visits)
4. THE Ratio_Slider SHALL display the current ratio as localized text showing base days and home days (e.g., "5:2 ימים בסיס/בית" in Hebrew)
5. THE Ratio_Slider SHALL have a bounded range where the center is the Optimal_Ratio, the left limit is the minimum home days (1 day), and the right limit is the maximum base days (all base, no home)
6. WHEN the Admin adjusts the Ratio_Slider, THE Home_Leave_System SHALL convert the selected ratio into solver-compatible parameters (eligibility_threshold_hours and balance_value)
7. THE Ratio_Slider SHALL use a gradient background with colors indicating the conservative-to-generous spectrum
8. THE Home_Leave_System SHALL eliminate the min_rest_hours configuration field and auto-compute rest requirements from the selected ratio (rest hours = 0 when using day-based ratios, since the eligibility threshold inherently provides rest time)

### Requirement 4: Manual Override Mode

**User Story:** As an Admin, I want to explicitly set the exact number of days at base and days at home, so that I have direct control when the automatic calculation does not fit my needs.

#### Acceptance Criteria

1. WHILE Manual_Mode is active, THE Home_Leave_System SHALL display numeric input fields for days at base and days at home
2. WHEN the Admin enters a base:home configuration, THE Feasibility_Engine SHALL evaluate whether the configuration satisfies Coverage_Requirement in real-time
3. IF the configuration is feasible and more home days could be granted, THEN THE Feasibility_Engine SHALL display a green indicator with a suggestion showing the maximum feasible home days
4. IF the configuration is not feasible, THEN THE Feasibility_Engine SHALL display a red indicator explaining that the Admin must reduce home days or increase base days to maintain coverage
5. THE Home_Leave_System SHALL convert the manually specified days into solver-compatible parameters (eligibility_threshold_hours and balance_value)
6. THE Home_Leave_System SHALL validate that days at base is at least 1 and days at home is at least 1
7. THE Feasibility_Engine SHALL update feedback within 500 milliseconds of the Admin changing a value

### Requirement 5: Feasibility Feedback

**User Story:** As an Admin, I want real-time feedback on whether my configuration works, so that I can avoid setting up impossible schedules.

#### Acceptance Criteria

1. WHEN the Admin changes the base:home ratio in either Automatic_Mode or Manual_Mode, THE Feasibility_Engine SHALL compute feasibility within 2 seconds
2. THE Feasibility_Engine SHALL determine feasibility by verifying: (member_count - leave_capacity) >= Coverage_Requirement at all times during the cycle
3. IF the configuration is feasible, THEN THE Feasibility_Engine SHALL display a green indicator with the effective ratio and number of people at home per cycle
4. IF the configuration is not feasible, THEN THE Feasibility_Engine SHALL display a red indicator with a localized explanation of why coverage cannot be maintained
5. THE Feasibility_Engine SHALL reuse the existing preview endpoint mechanism to compute feasibility without persisting changes
6. THE Feasibility_Engine SHALL debounce requests with a 500-millisecond interval to avoid excessive server calls during rapid slider movement

### Requirement 6: Emergency Freeze Activation

**User Story:** As an Admin, I want to immediately freeze all home-leave when an emergency occurs, so that all personnel remain at base.

#### Acceptance Criteria

1. WHEN the Admin activates Emergency_Freeze, THE Home_Leave_System SHALL immediately prevent any new home-leave assignments for the group
2. WHILE Emergency_Freeze is active, THE Solver SHALL cancel any pending home-leave that has not yet started (future presence windows)
3. WHILE Emergency_Freeze is active, THE Home_Leave_System SHALL display a prominent visual indicator that home-leave is frozen
4. WHILE Emergency_Freeze is active, THE Home_Leave_System SHALL present an option to the Admin: "Use frozen personnel for task scheduling?"
5. IF the Admin selects "yes" for the scheduling option, THEN THE Solver SHALL include the frozen personnel in automatic task assignment calculations
6. IF the Admin does not select the scheduling option, THEN THE Solver SHALL exclude the frozen personnel from automatic task assignments while keeping them at base (the Admin can still manually assign them through the schedule)
7. THE Home_Leave_System SHALL persist the Emergency_Freeze state and scheduling option so they survive server restarts

### Requirement 7: Emergency Freeze Deactivation and Recovery

**User Story:** As an Admin, I want the system to resume normal home-leave rotation after an emergency ends, so that personnel who were owed leave still receive it.

#### Acceptance Criteria

1. WHEN the Admin deactivates Emergency_Freeze, THE Home_Leave_System SHALL restore the previously active mode (Automatic_Mode or Manual_Mode) with its last-saved parameters
2. WHEN Emergency_Freeze is deactivated, THE Home_Leave_System SHALL preserve the Leave_Queue state that existed at freeze activation and resume from that point
3. WHEN Emergency_Freeze is deactivated, THE Solver SHALL prepare a new schedule that accounts for owed home-leave accumulated during the freeze
4. THE Cumulative_Tracker SHALL continue accumulating consecutive_hours_at_base during Emergency_Freeze so that owed leave is reflected in priority after deactivation
5. WHILE Emergency_Freeze is active, THE Home_Leave_System SHALL record the freeze start timestamp and freeze duration for recovery calculations
6. WHEN Emergency_Freeze is deactivated, THE Home_Leave_System SHALL trigger a solver run to recalculate the schedule and resume the home-leave rotation

### Requirement 8: Solver Parameter Translation

**User Story:** As a developer, I want the new ratio-based configuration to translate cleanly into existing solver parameters, so that the solver logic does not require fundamental changes.

#### Acceptance Criteria

1. THE Home_Leave_System SHALL translate base_days into eligibility_threshold_hours using the formula: eligibility_threshold_hours = base_days × 24
2. THE Home_Leave_System SHALL translate the slider position into a balance_value (0–100) that maps to the solver's existing weight system (weight = balance_value × 4)
3. THE Home_Leave_System SHALL preserve Leave_Duration as a separate configurable parameter independent of the ratio
4. THE Home_Leave_System SHALL set min_rest_hours to 0 when using the new day-based ratio system, since the eligibility threshold inherently provides sufficient rest time
5. WHEN Emergency_Freeze is active with scheduling option "yes", THE Home_Leave_System SHALL set balance_value to 0 (no home-leave preference) in the solver payload
6. WHEN Emergency_Freeze is active with scheduling option "no", THE Home_Leave_System SHALL omit home_leave_config from the solver payload entirely

### Requirement 9: Leave Duration Configuration

**User Story:** As an Admin, I want to separately configure how long each home visit lasts, so that I can control visit length independently from the rotation ratio.

#### Acceptance Criteria

1. THE Home_Leave_System SHALL display a Leave_Duration input field in Automatic_Mode and Manual_Mode
2. THE Home_Leave_System SHALL accept Leave_Duration values between 12 and 168 hours (0.5 to 7 days)
3. WHEN Leave_Duration changes, THE Home_Leave_System SHALL recalculate the Optimal_Ratio in Automatic_Mode
4. THE Home_Leave_System SHALL display Leave_Duration in day units for the Admin (e.g., "2 ימים" in Hebrew, "2 days" in English) while storing it internally as hours

### Requirement 10: Data Migration

**User Story:** As a developer, I want existing home-leave configurations to migrate seamlessly to the new system, so that groups with existing settings continue to function correctly.

#### Acceptance Criteria

1. WHEN the system upgrades, THE Home_Leave_System SHALL migrate existing configurations by: setting mode to Automatic_Mode, computing base_days from existing eligibility_threshold_hours (base_days = eligibility_threshold_hours / 24), and preserving existing Leave_Duration and balance_value
2. THE Home_Leave_System SHALL add new database columns (mode, emergency_freeze_active, emergency_use_for_scheduling, freeze_started_at, pre_freeze_mode) with safe defaults that preserve current behavior
3. IF an existing configuration has eligibility_threshold_hours that does not divide evenly into days, THEN THE Home_Leave_System SHALL round to the nearest whole day
4. THE Home_Leave_System SHALL set min_rest_hours to 0 for all migrated configurations since the new system handles rest implicitly

### Requirement 11: UI Localization

**User Story:** As an Admin, I want all home-leave controls to display in my chosen language with correct text direction, so that the interface is consistent with the rest of the application.

#### Acceptance Criteria

1. THE Home_Leave_System SHALL render all labels, tooltips, and feedback messages in the user's selected language (Hebrew, English, or Russian)
2. WHILE the user's language is Hebrew, THE Ratio_Slider SHALL render in RTL direction with the gradient, labels, and thumb position mirrored accordingly
3. WHILE the user's language is English or Russian, THE Ratio_Slider SHALL render in LTR direction
4. THE Ratio_Slider gradient, labels, and color indicators SHALL correctly align with the text direction of the active language
5. THE Home_Leave_System SHALL use locale-appropriate number formatting for day counts displayed to the Admin
6. THE Home_Leave_System SHALL use the existing i18n infrastructure for all translatable strings

### Requirement 12: Scalability

**User Story:** As a developer, I want the optimal ratio calculation and feasibility checks to perform well with large groups, so that the system remains responsive regardless of group size.

#### Acceptance Criteria

1. THE Home_Leave_System SHALL compute the Optimal_Ratio within 500 milliseconds for groups of up to 50 members
2. THE Feasibility_Engine SHALL return feasibility results within 2 seconds for groups of up to 50 members
3. THE Home_Leave_System SHALL compute the Optimal_Ratio using a formula-based approach (not solver invocation) to ensure consistent performance
4. THE Feasibility_Engine SHALL use the existing preview endpoint with preview_mode (3-second solver time limit) for detailed feasibility checks
