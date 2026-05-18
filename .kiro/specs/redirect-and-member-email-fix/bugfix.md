# Bugfix Requirements Document

## Introduction

This bugfix addresses three related UX issues: (1) the pricing page's back link incorrectly sends users to the login page, (2) after login or space selection the app redirects to the obsolete `/schedule/today` route instead of the groups page, and (3) the member details modal does not display or allow editing of a member's email address. Together these issues degrade navigation flow and limit admin capabilities.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a user presses the "back" link on the pricing page THEN the system navigates to `/login` regardless of whether the user is authenticated or where they came from

1.2 WHEN a user selects a space on the spaces page (or has only one space and is auto-redirected) THEN the system redirects to `/schedule/today` which is an obsolete single-group schedule view

1.3 WHEN an admin opens the member details modal for a group member THEN the system does not display the member's email address

1.4 WHEN an admin attempts to edit a member's details THEN the system does not provide an email field, making it impossible to view or update the member's email

### Expected Behavior (Correct)

2.1 WHEN a user presses the "back" link on the pricing page THEN the system SHALL navigate to the previous page in browser history (or to the landing page `/` if there is no history)

2.2 WHEN a user selects a space on the spaces page (or has only one space and is auto-redirected) THEN the system SHALL redirect to `/groups` (the "my groups" page)

2.3 WHEN an admin opens the member details modal for a group member THEN the system SHALL display the member's email address in the info section

2.4 WHEN an admin edits a non-admin member's details THEN the system SHALL allow the admin to update the member's email address

2.5 WHEN an admin attempts to edit another admin member's details THEN the system SHALL NOT allow editing the email (or any fields) — only the group owner can edit admin members

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a user navigates to the pricing page directly (e.g., from a bookmark or external link) THEN the system SHALL CONTINUE TO display the pricing page without requiring authentication

3.2 WHEN a user logs in with a `?redirect=` query parameter THEN the system SHALL CONTINUE TO redirect to the specified path after login

3.3 WHEN a user logs in without a `?redirect=` parameter THEN the system SHALL CONTINUE TO redirect to `/schedule/my-missions` as the default post-login destination

3.4 WHEN an admin edits a non-admin member's name, phone number, birthday, or profile image THEN the system SHALL CONTINUE TO save those fields correctly

3.5 WHEN a non-admin user views the member details modal THEN the system SHALL CONTINUE TO show member info in read-only mode without edit controls

3.6 WHEN the group owner edits an admin member's details THEN the system SHALL CONTINUE TO allow the edit to succeed
