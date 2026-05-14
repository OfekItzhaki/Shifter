# Bugfix Requirements Document

## Introduction

Multiple related bugs in the invitation/join flow prevent users from successfully joining groups via invitation links. The issues span the backend join-by-code handler (missing SpaceMembership creation), the frontend registration flow (lost redirect context after registration), and UX issues (401 error page shown instead of silent redirect). These bugs are blocking real users from joining groups and causing confusion during the auth flow.

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN a user joins a group via join code (POST /groups/join) THEN the system creates a Person and GroupMembership but does NOT create a SpaceMembership for the user, resulting in 403 errors on all subsequent space-scoped API calls because the PermissionService cannot find any permission grants for the user in that space

1.2 WHEN an unauthenticated user clicks an invitation link and is redirected to the register page, completes registration THEN the system redirects to `/login?registered=1` losing the original join redirect URL, so the user never completes the join flow after registering

1.3 WHEN a user receives a 401 response and the token refresh fails THEN the system displays a dedicated `/error/unauthorized` error page with a login button instead of silently redirecting the user to the landing/login page

1.4 WHEN an existing user is invited to a group by email or phone (AddPersonByEmail/AddPersonByPhone commands) THEN the system creates a Person and GroupMembership but does NOT create a SpaceMembership for the invited user, causing the same 403 issue as 1.1 when the invited user tries to access the group

### Expected Behavior (Correct)

2.1 WHEN a user joins a group via join code (POST /groups/join) THEN the system SHALL create a SpaceMembership record for the user in the group's space (if one does not already exist), in addition to the Person and GroupMembership records, so the user has basic space access and the SpaceView permission

2.2 WHEN an unauthenticated user clicks an invitation link, is redirected to the register page, and completes registration THEN the system SHALL preserve the original redirect URL through the registration flow and redirect the user back to the join page (with the code) after login, so the join flow completes end-to-end

2.3 WHEN a user receives a 401 response and the token refresh fails THEN the system SHALL silently redirect the user to the main landing page (/) or login page (/login) without showing an intermediate error page

2.4 WHEN an existing user is invited to a group by email or phone THEN the system SHALL create a SpaceMembership record for the invited user in the group's space (if one does not already exist), so the user can access the space immediately

### Unchanged Behavior (Regression Prevention)

3.1 WHEN a user who already has a SpaceMembership joins a group via join code THEN the system SHALL CONTINUE TO skip creating a duplicate SpaceMembership and only add the GroupMembership

3.2 WHEN a user is already a member of a group and attempts to join again via code THEN the system SHALL CONTINUE TO return success without creating duplicate memberships

3.3 WHEN a 403 (Forbidden) error occurs due to insufficient permissions within a space the user belongs to THEN the system SHALL CONTINUE TO show the forbidden error page (this is a legitimate access denial, not a missing membership)

3.4 WHEN a user registers without an invitation context (direct registration) THEN the system SHALL CONTINUE TO redirect to `/login?registered=1` and then to the default schedule page after login

3.5 WHEN a user logs in with valid credentials and a redirect parameter THEN the system SHALL CONTINUE TO redirect to the specified redirect URL after successful login

3.6 WHEN phone-only registration is used (no email provided) THEN the system SHALL CONTINUE TO create the account with a placeholder email and function correctly

3.7 WHEN WhatsApp invitations are sent via the existing Twilio integration THEN the system SHALL CONTINUE TO send messages using the configured Twilio WhatsApp sender
