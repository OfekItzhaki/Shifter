# Requirements Document

## Introduction

This document defines the requirements for the Onboarding Wizard in Shifter. The feature provides a guided, step-by-step experience for new users after their first registration and login. It helps users understand the core workflow of the scheduling application — creating a group, adding members, defining tasks/shifts, setting constraints, and running the solver — through an interactive checklist overlay. The onboarding is entirely frontend-driven, persists completion state in localStorage, supports all three application locales (en, he, ru), and is dismissible at any point.

## Glossary

- **Onboarding_Wizard**: The top-level UI component that orchestrates the multi-step guided experience for new users.
- **Onboarding_Step**: A single item in the onboarding checklist representing one action the user should complete (e.g., "Create your first group").
- **Onboarding_Checklist**: The persistent sidebar or panel displaying all Onboarding_Steps with their completion status.
- **Onboarding_Storage**: The localStorage-based persistence layer that tracks whether onboarding has been completed or dismissed for a given user.
- **Step_CTA**: The call-to-action button within an Onboarding_Step that navigates the user to the relevant page or triggers the relevant action.
- **Spaces_Page**: The page at `/spaces` where users land after login and where the onboarding is triggered.
- **AppShell**: The main layout wrapper component that renders navigation and content areas.

## Requirements

### Requirement 1: Trigger Onboarding for New Users

**User Story:** As a newly registered user, I want to see a guided onboarding experience after my first login, so that I understand how to set up my first schedule.

#### Acceptance Criteria

1. WHEN a user navigates to the Spaces_Page and has zero groups, THE Onboarding_Wizard SHALL display automatically
2. WHEN the Onboarding_Storage contains a completion or dismissal record for the current user, THE Onboarding_Wizard SHALL not display
3. WHEN a user has one or more existing groups, THE Onboarding_Wizard SHALL not display regardless of Onboarding_Storage state
4. THE Onboarding_Wizard SHALL check both Onboarding_Storage and group count before deciding to display

### Requirement 2: Display Onboarding Checklist

**User Story:** As a new user seeing the onboarding, I want to see a clear list of steps I need to complete, so that I know what to do and in what order.

#### Acceptance Criteria

1. THE Onboarding_Checklist SHALL display the following steps in order: create a group, add members to the group, define tasks or shifts, set constraints, run the schedule solver
2. THE Onboarding_Checklist SHALL display each Onboarding_Step with a title, a brief description, and a Step_CTA button
3. THE Onboarding_Checklist SHALL visually indicate which steps are completed and which are pending
4. THE Onboarding_Checklist SHALL highlight the current recommended step (the first incomplete step)
5. WHEN all steps are marked complete, THE Onboarding_Checklist SHALL display a success state with a congratulatory message

### Requirement 3: Step Navigation via CTA

**User Story:** As a new user following the onboarding, I want each step to have a clear action button that takes me to the right place, so that I can complete the setup without searching for features.

#### Acceptance Criteria

1. WHEN a user clicks the Step_CTA for "create a group", THE Onboarding_Wizard SHALL navigate the user to the group creation flow
2. WHEN a user clicks the Step_CTA for "add members", THE Onboarding_Wizard SHALL navigate the user to the people/members page for their group
3. WHEN a user clicks the Step_CTA for "define tasks", THE Onboarding_Wizard SHALL navigate the user to the tasks management page
4. WHEN a user clicks the Step_CTA for "set constraints", THE Onboarding_Wizard SHALL navigate the user to the constraints page
5. WHEN a user clicks the Step_CTA for "run solver", THE Onboarding_Wizard SHALL navigate the user to the schedule generation page
6. WHEN a user completes the action associated with a step and returns to the Onboarding_Checklist, THE Onboarding_Wizard SHALL mark that step as complete

### Requirement 4: Step Completion Detection

**User Story:** As a new user progressing through onboarding, I want steps to be marked complete automatically when I finish the associated action, so that I can track my progress without manual effort.

#### Acceptance Criteria

1. WHEN the user creates their first group, THE Onboarding_Wizard SHALL mark the "create a group" step as complete
2. WHEN the user adds at least one member to any group, THE Onboarding_Wizard SHALL mark the "add members" step as complete
3. WHEN the user creates at least one task or shift definition, THE Onboarding_Wizard SHALL mark the "define tasks" step as complete
4. WHEN the user creates at least one constraint, THE Onboarding_Wizard SHALL mark the "set constraints" step as complete
5. WHEN the user triggers a schedule solver run, THE Onboarding_Wizard SHALL mark the "run solver" step as complete
6. THE Onboarding_Wizard SHALL persist step completion state in Onboarding_Storage so progress survives page refreshes

### Requirement 5: Dismiss Onboarding

**User Story:** As a user who already knows how to use the app, I want to dismiss the onboarding at any time, so that it does not block my workflow.

#### Acceptance Criteria

1. THE Onboarding_Wizard SHALL display a dismiss button that is visible at all times during onboarding
2. WHEN a user clicks the dismiss button, THE Onboarding_Wizard SHALL close immediately
3. WHEN a user dismisses the onboarding, THE Onboarding_Storage SHALL record the dismissal for the current user
4. WHEN the Onboarding_Storage contains a dismissal record, THE Onboarding_Wizard SHALL not display on subsequent page loads

### Requirement 6: Completion Persistence

**User Story:** As a user who has completed or dismissed the onboarding, I want the system to remember my choice, so that the onboarding does not reappear.

#### Acceptance Criteria

1. THE Onboarding_Storage SHALL store onboarding state using a localStorage key scoped to the user ID
2. THE Onboarding_Storage SHALL persist one of three states: in-progress (with step completion data), completed, or dismissed
3. WHEN all five onboarding steps are marked complete, THE Onboarding_Storage SHALL transition the state to completed
4. IF localStorage is unavailable or throws an error, THEN THE Onboarding_Wizard SHALL still function without persistence and default to showing the onboarding

### Requirement 7: Internationalization Support

**User Story:** As a user with a non-English locale preference, I want the onboarding content to appear in my language and respect my text direction, so that I can understand the guidance.

#### Acceptance Criteria

1. THE Onboarding_Wizard SHALL render all text content (step titles, descriptions, buttons, messages) using next-intl translation keys
2. WHILE the user's locale is set to "he", THE Onboarding_Wizard SHALL render in right-to-left layout direction
3. THE Onboarding_Wizard SHALL support all three application locales: en, he, and ru
4. THE Onboarding_Wizard SHALL use logical CSS properties (start/end) instead of physical properties (left/right) for directional styling

### Requirement 8: Responsive and Accessible Design

**User Story:** As a user on any device or using assistive technology, I want the onboarding to be usable and accessible, so that I can complete setup regardless of my device or abilities.

#### Acceptance Criteria

1. THE Onboarding_Wizard SHALL be fully usable on viewport widths from 320px to 1920px
2. THE Onboarding_Wizard SHALL use semantic HTML elements and ARIA attributes for screen reader compatibility
3. THE Onboarding_Wizard SHALL support keyboard navigation — all interactive elements are focusable and activatable via keyboard
4. THE Onboarding_Wizard SHALL maintain a minimum color contrast ratio of 4.5:1 for all text content
5. WHEN the Onboarding_Wizard is displayed as an overlay, THE Onboarding_Wizard SHALL trap focus within the overlay until dismissed or closed

### Requirement 9: Visual Design and UX

**User Story:** As a new user, I want the onboarding to feel modern, clean, and non-overwhelming, so that I am guided without feeling pressured.

#### Acceptance Criteria

1. THE Onboarding_Wizard SHALL use the existing Tailwind CSS design system and color palette from the application
2. THE Onboarding_Wizard SHALL display one step at a time as the primary focus while showing overall progress
3. THE Onboarding_Wizard SHALL use smooth transitions between states (step completion, dismissal)
4. THE Onboarding_Wizard SHALL not block the entire viewport — the user can still see the application behind the onboarding panel
5. THE Onboarding_Wizard SHALL use progressive disclosure — showing detailed step information only when that step is active

### Requirement 10: Re-access Onboarding

**User Story:** As a user who dismissed the onboarding early, I want to be able to re-access it if I change my mind, so that I can get guidance later.

#### Acceptance Criteria

1. THE AppShell SHALL display a help or onboarding menu item that allows the user to restart the onboarding
2. WHEN a user triggers the restart action, THE Onboarding_Storage SHALL reset the state to in-progress with all steps marked pending
3. WHEN the onboarding is restarted, THE Onboarding_Wizard SHALL re-evaluate step completion based on current application state (existing groups, members, tasks, constraints, solver runs)
