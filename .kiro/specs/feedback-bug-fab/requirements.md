# Requirements Document

## Introduction

A floating action button (FAB) split into two halves — bug report and feedback — that appears on every page of the Shifter web application. Clicking either half opens a shared modal with a form. On submission, the system sends an email to the developer with the appropriate subject line indicating the submission type.

## Glossary

- **FAB**: The floating action button component rendered in a fixed position on screen, split into two clickable halves (bug and feedback).
- **Submission_Modal**: The modal dialog containing the bug/feedback form, shared by both submission types.
- **Submission_Type**: An enum-like value distinguishing between "bug" and "feedback" submissions.
- **Feedback_API**: The backend API endpoint in Jobuler.Api that receives form data and dispatches an email.
- **Developer_Email**: The configured recipient email address for all bug reports and feedback submissions.

## Requirements

### Requirement 1: FAB Visibility

**User Story:** As a user, I want to see a floating action button on every page, so that I can quickly report a bug or provide feedback from anywhere in the application.

#### Acceptance Criteria

1. THE FAB SHALL render in a fixed position at the bottom-left corner of the viewport, offset 16px from the left edge and 16px from the bottom edge, on all authenticated pages.
2. WHILE the user scrolls the page content, THE FAB SHALL remain visible in its fixed viewport position.
3. THE FAB SHALL display a bug icon on the left half and a feedback icon on the right half, with a visible divider separating the two halves.
4. THE FAB SHALL be rendered above all non-modal page content using a z-index of at least 1000.
5. THE FAB SHALL have a minimum clickable area of 44×44 CSS pixels per half.

### Requirement 2: FAB Interaction

**User Story:** As a user, I want to click either half of the FAB to open the appropriate form, so that I can submit a bug report or feedback.

#### Acceptance Criteria

1. WHEN the user clicks the bug half of the FAB, THE Submission_Modal SHALL become visible with Submission_Type set to "bug".
2. WHEN the user clicks the feedback half of the FAB, THE Submission_Modal SHALL become visible with Submission_Type set to "feedback".
3. IF Submission_Type is "bug", THEN THE Submission_Modal SHALL display "Bug Report" as the title.
4. IF Submission_Type is "feedback", THEN THE Submission_Modal SHALL display "Feedback" as the title.
5. IF the Submission_Modal is already open WHEN the user clicks either half of the FAB, THEN THE Submission_Modal SHALL remain open and update the Submission_Type and title to match the clicked half.

### Requirement 3: Submission Form

**User Story:** As a user, I want to describe my issue or feedback in a form, so that the developer receives a clear description of what I want to communicate.

#### Acceptance Criteria

1. THE Submission_Modal SHALL contain a multi-line text field for the user to enter a description, with a maximum input length of 5000 characters.
2. THE Submission_Modal SHALL contain a submit button.
3. WHILE the description field contains only whitespace or is empty, THE Submission_Modal SHALL disable the submit button.
4. THE Submission_Modal SHALL contain a close button or allow dismissal by clicking outside the modal.
5. WHEN the user closes the modal without submitting, THE Submission_Modal SHALL discard the entered text.
6. THE Submission_Modal SHALL display a character count indicator showing the number of characters entered out of the 5000 character maximum.

### Requirement 4: Form Submission

**User Story:** As a user, I want my submission to be sent when I click submit, so that the developer receives my report.

#### Acceptance Criteria

1. WHEN the user clicks the submit button, THE Submission_Modal SHALL send the description text and the current Submission_Type to the Feedback_API endpoint.
2. WHILE the submission is in progress, THE Submission_Modal SHALL display a loading indicator and disable the submit button.
3. WHEN the Feedback_API returns a success response, THE Submission_Modal SHALL display a success message and close automatically after 2 seconds.
4. IF the Feedback_API returns an error, THEN THE Submission_Modal SHALL display an error message and keep the form open with the entered text preserved.
5. IF the Feedback_API does not respond within 10 seconds, THEN THE Submission_Modal SHALL treat the request as failed, display an error message indicating a timeout, and keep the form open with the entered text preserved.

### Requirement 5: Backend Email Dispatch

**User Story:** As a developer, I want to receive an email for each bug report or feedback submission, so that I can track and address user issues.

#### Acceptance Criteria

1. WHEN the Feedback_API receives a valid submission, THE Feedback_API SHALL send an email to the configured Developer_Email address using the existing IEmailSender service.
2. WHEN Submission_Type is "bug", THE Feedback_API SHALL set the email subject to "Bug Report: " followed by the first 50 characters of the description, or the full description if it contains fewer than 50 characters.
3. WHEN Submission_Type is "feedback", THE Feedback_API SHALL set the email subject to "Feedback: " followed by the first 50 characters of the description, or the full description if it contains fewer than 50 characters.
4. THE Feedback_API SHALL include the full description text and the authenticated user's email address as sender reference in the email body passed as the htmlBody parameter to IEmailSender.
5. IF the email dispatch fails, THEN THE Feedback_API SHALL log the error and return a 500 status code with an error message indicating that the submission could not be processed.
6. IF the description contains HTML markup, THEN THE Feedback_API SHALL escape the HTML entities before including it in the email body.

### Requirement 6: API Endpoint Security

**User Story:** As a developer, I want the feedback endpoint to be protected, so that only authenticated users can submit reports.

#### Acceptance Criteria

1. THE Feedback_API SHALL require authentication via the existing JWT bearer scheme by applying the [Authorize] attribute.
2. IF a request is received without a valid JWT bearer token, THEN THE Feedback_API SHALL return a 401 status code with no response body.
3. THE Feedback_API SHALL validate that the description field is present and contains between 1 and 5000 characters after trimming leading and trailing whitespace.
4. THE Feedback_API SHALL validate that the submission_type field is present and contains one of the allowed values: "bug" or "feedback".
5. IF validation of any field fails, THEN THE Feedback_API SHALL return a 400 status code with a response body containing the field name and the reason for rejection for each invalid field.

### Requirement 7: Rate Limiting

**User Story:** As a developer, I want to prevent abuse of the feedback endpoint, so that the system is not overwhelmed by excessive submissions.

#### Acceptance Criteria

1. THE Feedback_API SHALL limit each authenticated user to 5 submissions per sliding 60-minute window, counting only submissions that received a success response.
2. IF the rate limit is exceeded, THEN THE Feedback_API SHALL return a 429 status code with a Retry-After header indicating the number of seconds until the oldest submission in the window expires.
3. IF the Feedback_API returns a 429 status code, THEN THE Submission_Modal SHALL display a message indicating the user has reached the submission limit and should try again after the duration specified in the Retry-After header.

### Requirement 8: Accessibility

**User Story:** As a user with assistive technology, I want the FAB and modal to be accessible, so that I can use the feature regardless of my abilities.

#### Acceptance Criteria

1. THE FAB SHALL have a distinct aria-label on each half that identifies the submission type: one indicating bug reporting and one indicating feedback submission.
2. WHILE the Submission_Modal is open, THE Submission_Modal SHALL constrain keyboard focus so that Tab and Shift+Tab cycle only through focusable elements within the modal.
3. WHILE the Submission_Modal is open, THE Submission_Modal SHALL close when the user presses the Escape key.
4. THE Submission_Modal form fields SHALL have associated label elements linked via matching for/id attributes.
5. THE FAB halves SHALL each be keyboard-focusable and activatable via Enter or Space key.
6. WHEN the Submission_Modal closes, THE Submission_Modal SHALL return keyboard focus to the FAB half that triggered the modal.
7. WHEN the Submission_Modal opens, THE Submission_Modal SHALL have a dialog role and an accessible name matching the modal title so that screen readers announce it.
