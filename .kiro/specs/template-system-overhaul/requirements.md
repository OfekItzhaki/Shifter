# Requirements Document

## Introduction

Overhaul the Shifter group template system to make the platform truly generic — not limited to Army or Restaurant use cases. This feature introduces template-driven feature visibility, removes dead code (DislikedHatedScore), generalizes hardcoded constraints (max_kitchen_per_week → max_task_type_per_period), removes the hardcoded IsKitchenTask() check, removes min_rest_hours from template seed data, and persists the template type on the Group entity so the UI can show/hide features dynamically.

## Glossary

- **Shifter**: The scheduling platform brand name
- **Group**: A scheduling unit within a Space (e.g., a platoon, restaurant team, hospital ward)
- **Template_Type**: An enum value persisted on a Group that determines which features and settings are visible to the admin. Values: Army, Restaurant, Hospital, Security, Custom
- **Feature_Visibility_Map**: A configuration that maps each Template_Type to the set of UI features and settings shown to the group admin
- **Closed_Base**: A group setting indicating members reside on-site (relevant for military/security, hidden for restaurants)
- **Home_Leave**: A feature for tracking consecutive hours at base and granting leave (relevant for military, hidden for restaurants)
- **CumulativeRecord**: A per-person entity tracking assignment counters across multiple time windows for fairness
- **CumulativeTracker**: An infrastructure service that computes assignment deltas after schedule publication
- **Constraint_Rule**: A persisted rule that the solver enforces (e.g., min rest hours, max task type per period)
- **Task_Type_Counter**: A generic per-person counter tracking how many times a person was assigned to a specific task type within a time window
- **Admin**: A user with management permissions for a Group

## Requirements

### Requirement 1: Persist Template Type on Group Entity

**User Story:** As an admin, I want the template type to be stored on my group, so that the platform remembers which features to show me.

#### Acceptance Criteria

1. WHEN a Group is created with a template selection, THE Group SHALL store the selected Template_Type as a persisted property
2. THE Group SHALL support the following Template_Type values: Army, Restaurant, Hospital, Security, Custom
3. WHEN no template is explicitly selected, THE Group SHALL default the Template_Type to Custom
4. WHEN an admin changes the Template_Type on an existing Group, THE Group SHALL update the persisted value and apply the new Feature_Visibility_Map immediately
5. THE Group SHALL expose the Template_Type via the API response so the frontend can determine feature visibility

### Requirement 2: Template-Driven Feature Visibility

**User Story:** As an admin, I want the UI to show only the features relevant to my group type, so that I am not confused by irrelevant settings.

#### Acceptance Criteria

1. WHILE the Group Template_Type is Restaurant, THE Feature_Visibility_Map SHALL hide the Closed_Base toggle and all Home_Leave configuration options
2. WHILE the Group Template_Type is Army, THE Feature_Visibility_Map SHALL show the Closed_Base toggle, Home_Leave configuration, minimum rest between shifts, and minimum people at base settings
3. WHILE the Group Template_Type is Hospital, THE Feature_Visibility_Map SHALL show the Closed_Base toggle as optional, and show qualification requirements prominently
4. WHILE the Group Template_Type is Security, THE Feature_Visibility_Map SHALL show the Closed_Base toggle and minimum rest between shifts, and hide Home_Leave configuration by default
5. WHILE the Group Template_Type is Custom, THE Feature_Visibility_Map SHALL show all available features and settings so the admin can pick what they need
6. THE Feature_Visibility_Map SHALL be defined as a frontend configuration keyed by Template_Type, requiring no API call to resolve visibility

### Requirement 3: Remove DislikedHatedScore Dead Code

**User Story:** As a developer, I want dead code removed from the system, so that the codebase remains clean and maintainable.

#### Acceptance Criteria

1. THE CumulativeRecord SHALL NOT contain DislikedHatedScore properties (DislikedHatedScore7d, DislikedHatedScore14d, DislikedHatedScore30d, DislikedHatedScore90d, DislikedHatedScorePeriod)
2. THE AssignmentCountsDelta value object SHALL NOT contain a DislikedHatedScore field
3. THE CumulativeTracker SHALL NOT compute or pass a DislikedHatedScore value when building deltas
4. THE database migration SHALL drop the disliked_hated_score columns from the cumulative_records table
5. THE solver payload normalizer SHALL NOT include DislikedHatedScore in the fairness counters sent to the solver
6. THE FairnessCounter entity SHALL NOT contain a DislikedHatedScore7d property
7. THE GetCumulativeStatsQuery and GetBurdenStatsQuery SHALL NOT return DislikedHatedScore in their response DTOs

### Requirement 4: Generalize max_kitchen_per_week to max_task_type_per_period

**User Story:** As an admin, I want to limit how many times a person is assigned to any task type within a configurable period, so that I can enforce fairness rules beyond just kitchen duty.

#### Acceptance Criteria

1. THE Constraint_Rule system SHALL support a rule type named max_task_type_per_period with payload parameters: task_type_name (string), max (number), period_days (number)
2. WHEN a max_task_type_per_period constraint is active, THE solver SHALL enforce that no person exceeds the specified max assignments for the named task type within the specified period_days window
3. THE admin UI SHALL allow the admin to select any task type defined in the group and set a max count and period in days
4. WHEN existing max_kitchen_per_week constraints are present in the database, THE migration SHALL convert them to max_task_type_per_period with task_type_name="kitchen" and period_days=7
5. THE system SHALL remove all references to the max_kitchen_per_week rule type from the codebase after migration

### Requirement 5: Remove Hardcoded IsKitchenTask Check

**User Story:** As a developer, I want the kitchen-specific tracking logic removed, so that the system tracks task type counts generically.

#### Acceptance Criteria

1. THE CumulativeTracker SHALL NOT contain an IsKitchenTask method or any hardcoded task name comparison for "מטבח" or "kitchen"
2. THE CumulativeRecord SHALL NOT contain KitchenCount properties (KitchenCount7d, KitchenCount14d, KitchenCount30d, KitchenCount90d, KitchenCountPeriod)
3. WHEN a schedule version is published, THE CumulativeTracker SHALL compute per-person assignment counts grouped by task type name generically
4. THE database migration SHALL drop the kitchen_count columns from the cumulative_records table
5. THE Task_Type_Counter data SHALL be stored in a separate table or JSON structure keyed by task type name, supporting any number of task types without schema changes

### Requirement 6: Remove min_rest_hours from Template Seed Data

**User Story:** As a developer, I want templates to stop seeding a min_rest_hours constraint, so that new groups rely on the group-level setting instead of a redundant constraint.

#### Acceptance Criteria

1. THE group template definitions SHALL NOT include a min_rest_hours entry in their constraints array
2. WHEN a new group is created from a template, THE system SHALL NOT create a min_rest_hours Constraint_Rule row
3. THE Group entity SHALL continue to enforce minimum rest between shifts via the existing MinRestBetweenShiftsHours property (already a group-level setting)

### Requirement 7: Template Selection During Group Creation

**User Story:** As an admin, I want to pick a template when creating a group, so that the group is pre-configured with relevant tasks, qualifications, and settings.

#### Acceptance Criteria

1. WHEN an admin creates a new group, THE system SHALL present the available templates: Army, Restaurant, Hospital, Security, Custom
2. WHEN a template is selected, THE system SHALL seed the group with the template's predefined tasks, qualifications, unavailability reasons, and solver horizon
3. WHEN the Custom template is selected, THE system SHALL create the group with no predefined tasks or constraints
4. THE template selection UI SHALL display each template with a name, localized name (Hebrew), description, icon, and color

### Requirement 8: Template-Aware Labels for Closed-Base / Stayover

**User Story:** As an admin, I want the "closed base" feature to be labeled appropriately for my group type, so that the terminology makes sense in my context.

#### Acceptance Criteria

1. WHILE the Group Template_Type is Army, THE UI SHALL label the stayover feature as "בסיס סגור" (Closed Base) in Hebrew and "Closed Base" in English
2. WHILE the Group Template_Type is Custom, THE UI SHALL label the stayover feature as "לינה במקום" (Stayover) in Hebrew and "Stayover" in English
3. WHILE the Group Template_Type is Hospital, THE UI SHALL label the stayover feature as "לינה במקום" (Stayover) in Hebrew and "Stayover" in English
4. WHILE the Group Template_Type is Security, THE UI SHALL label the stayover feature as "בסיס סגור" (Closed Base) in Hebrew and "Closed Base" in English
5. THE backend property name SHALL remain `IsClosedBase` for backward compatibility — only the UI label changes based on template
6. THE Feature_Visibility_Map SHALL include a `stayoverLabel` key per template that the frontend uses for rendering the toggle label and related section headers
