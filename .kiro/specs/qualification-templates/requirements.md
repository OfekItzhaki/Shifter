# Requirements Document

## Introduction

This feature extends the existing group template system to include default qualifications per industry template, and adds structured unavailability reasons (from templates) to the presence/unavailability workflow. Currently, qualifications are free-text entries added manually per group, and unavailability windows only have a free-text `Note` field. This feature introduces template-driven defaults for both, reducing setup friction and enabling structured reporting.

## Glossary

- **Qualification_Template**: A predefined set of qualification definitions associated with an industry template (e.g., Army, Restaurant, Security). Each entry includes a name and optional description.
- **Group_Qualification**: A qualification type defined at the group level. Members can be assigned qualifications from this list. Already exists in the system.
- **Unavailability_Reason**: A structured reason for marking a person as unavailable, selected from a configurable list rather than free-text.
- **Unavailability_Reason_Template**: A predefined set of unavailability reasons associated with an industry template.
- **Template_Picker**: The existing `GroupTemplatePicker` component that runs after group creation to apply industry-specific defaults (tasks, constraints, solver horizon).
- **Presence_Window**: An existing domain entity that tracks where a person physically is over a time window, including manual unavailability (state=AtHome).
- **Space**: The tenant-level container. All data is scoped to a space.
- **Group_Owner**: A user with management permissions over a group within a space.

## Requirements

### Requirement 1: Qualification Definitions in Templates

**User Story:** As a group owner, I want industry templates to include default qualifications, so that I do not have to manually create common qualification types from scratch when setting up a new group.

#### Acceptance Criteria

1. THE Qualification_Template SHALL define a list of qualification entries, where each entry contains a name (string, max 100 characters) and an optional description (string, max 500 characters).
2. WHEN the Template_Picker applies a template to a group, THE System SHALL create Group_Qualification records for each qualification entry defined in the selected Qualification_Template.
3. WHEN the Template_Picker applies a template that contains qualification entries, THE System SHALL create all qualification records within the same group and space context.
4. IF a qualification name from the template already exists in the group, THEN THE System SHALL skip that qualification entry without creating a duplicate.
5. WHEN the "Custom (Empty)" template is selected, THE System SHALL create zero default qualifications for the group.

### Requirement 2: Template Qualification Data per Industry

**User Story:** As a product owner, I want each industry template to ship with relevant default qualifications, so that groups in different industries get meaningful starting points.

#### Acceptance Criteria

1. THE Qualification_Template for "Army / Military Base" SHALL include qualifications: "Combat Medic", "Radio Operator", "Driver", "Commander", "Sharpshooter".
2. THE Qualification_Template for "Restaurant / Cafe" SHALL include qualifications: "Bartender", "Waiter", "Cook", "Shift Manager", "Barista".
3. THE Qualification_Template for "Hospital / Clinic" SHALL include qualifications: "Nurse", "Doctor", "Paramedic", "Lab Technician", "Receptionist".
4. THE Qualification_Template for "Security / Guard Service" SHALL include qualifications: "Armed Guard", "CCTV Operator", "Patrol", "Shift Supervisor", "First Aid Certified".

### Requirement 3: Group Owner Retains Full Qualification Control

**User Story:** As a group owner, I want to add, edit, and remove qualifications after a template is applied, so that I can customize the list to my specific needs.

#### Acceptance Criteria

1. AFTER a template is applied, THE System SHALL allow the Group_Owner to add new qualifications to the group using the existing create endpoint.
2. AFTER a template is applied, THE System SHALL allow the Group_Owner to edit the name and description of any qualification (including template-created ones) using the existing update endpoint.
3. AFTER a template is applied, THE System SHALL allow the Group_Owner to deactivate any qualification (including template-created ones) using the existing deactivate endpoint.
4. THE System SHALL treat template-created qualifications identically to manually-created qualifications after creation.

### Requirement 4: Unavailability Reason List per Space

**User Story:** As a group owner, I want a configurable list of unavailability reasons for my space, so that when marking someone as unavailable the reason is structured and consistent.

#### Acceptance Criteria

1. THE System SHALL store a list of Unavailability_Reason entries per space, where each entry contains an id, a display name (string, max 100 characters), and a sort order (integer).
2. THE System SHALL support a maximum of 50 Unavailability_Reason entries per space.
3. WHEN a space has no configured unavailability reasons, THE System SHALL return an empty list.
4. THE System SHALL allow the Group_Owner to create, update, reorder, and deactivate Unavailability_Reason entries via API endpoints.
5. THE System SHALL enforce tenant isolation by scoping all Unavailability_Reason queries to the current space.

### Requirement 5: Unavailability Reasons from Templates

**User Story:** As a group owner, I want industry templates to include default unavailability reasons, so that common reasons are pre-populated when I set up a new group.

#### Acceptance Criteria

1. THE Unavailability_Reason_Template SHALL define a list of reason entries per industry template, where each entry contains a display name.
2. WHEN the Template_Picker applies a template to a group, THE System SHALL create Unavailability_Reason entries in the space if the space does not already have any configured reasons.
3. IF the space already has one or more Unavailability_Reason entries, THEN THE System SHALL skip reason creation to avoid duplicating existing configuration.
4. THE Unavailability_Reason_Template for all industry templates SHALL include at minimum: "חופשה" (Vacation), "מחלה" (Sick), "אישי" (Personal), "לימודים" (Studies).

### Requirement 6: Custom Reason Option

**User Story:** As a group owner, I want a "custom reason" option when marking someone as unavailable, so that I can handle edge cases not covered by the predefined list.

#### Acceptance Criteria

1. WHEN displaying the unavailability reason selection, THE System SHALL include a "Custom" option at the end of the reason list.
2. WHEN the user selects the "Custom" option, THE System SHALL display a free-text input field for entering a custom reason (max 200 characters).
3. WHEN a custom reason is submitted, THE System SHALL store the free-text value alongside the presence window record.
4. THE System SHALL distinguish between a predefined reason (stored as a reference to Unavailability_Reason id) and a custom reason (stored as free text).

### Requirement 7: Unavailability Reason Attached to Presence Windows

**User Story:** As a group owner, I want each unavailability entry to have a structured reason, so that I can understand and report on why people are unavailable.

#### Acceptance Criteria

1. WHEN creating a manual Presence_Window with state AtHome, THE System SHALL accept an optional unavailability reason (either a predefined reason id or a custom text).
2. WHEN retrieving presence windows, THE System SHALL include the associated unavailability reason in the response payload.
3. THE System SHALL allow presence windows to have no reason (backward compatibility with existing data).
4. IF an unavailability reason id is provided that does not exist in the space, THEN THE System SHALL reject the request with a validation error.

### Requirement 8: Solver Compatibility

**User Story:** As a system operator, I want the solver to continue functioning correctly with the new unavailability reasons, so that scheduling is not disrupted.

#### Acceptance Criteria

1. THE Solver_Payload_Normalizer SHALL continue to include presence windows in the solver payload regardless of whether a reason is attached.
2. THE System SHALL not pass unavailability reason data to the solver (the solver only needs time windows and states, not reasons).
3. WHEN a presence window has an unavailability reason, THE Solver_Payload_Normalizer SHALL produce the same output as a presence window without a reason.
