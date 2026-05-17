# Requirements Document

## Introduction

Personal cross-group conflict detection for the Shifter scheduling platform. When a user logs in or when a schedule is published, the system checks whether that user has overlapping or insufficiently-spaced assignments across all groups they belong to (within the same space or across spaces via LinkedUserId). Only the individual user receives a notification — no group data is exposed to other groups. The solver continues to run per-group independently; conflicts are detected post-facto.

## Glossary

- **Conflict_Detector**: The service responsible for identifying scheduling conflicts across groups for a single user.
- **Assignment**: A record linking a Person to a TaskSlot within a ScheduleVersion (table: `assignments`).
- **TaskSlot**: A time-bounded slot (StartsAt, EndsAt) representing a shift or duty within a group.
- **Person**: An individual within a Space who can be a member of multiple Groups via GroupMembership.
- **LinkedUserId**: An optional field on Person that links the person record to an authenticated user, enabling cross-space identity resolution.
- **MinRestBetweenShiftsHours**: A per-group setting (default 8) defining the minimum required gap in hours between consecutive shifts.
- **Overlap_Conflict**: Two assignments where the TaskSlot time ranges intersect (one starts before the other ends).
- **Rest_Violation**: Two assignments from different groups where the gap between the end of one TaskSlot and the start of the next is less than the applicable MinRestBetweenShiftsHours.
- **Published_Version**: A ScheduleVersion with status Published — the currently active schedule for a group.
- **Space**: The top-level tenant container for people, groups, tasks, and roles.
- **Group**: A scheduling unit within a Space; groups are isolated from each other.

## Requirements

### Requirement 1: Trigger on Schedule Publish

**User Story:** As a user assigned to multiple groups, I want the system to check for conflicts in my schedule whenever a new version is published, so that I am immediately aware of double-bookings.

#### Acceptance Criteria

1. WHEN a ScheduleVersion is published, THE Conflict_Detector SHALL identify all persons who have at least one assignment in the newly published version.
2. WHEN a ScheduleVersion is published, THE Conflict_Detector SHALL check each identified person's assignments across all currently published versions in all groups the person belongs to, and flag a conflict whenever two or more assignments from different groups have overlapping time ranges (i.e., assignment A starts before assignment B ends AND assignment B starts before assignment A ends).
3. IF a person has a LinkedUserId, THEN THE Conflict_Detector SHALL also check assignments from published versions in other spaces where a Person record shares the same LinkedUserId.
4. WHEN the Conflict_Detector identifies one or more conflicts for a person, THE Conflict_Detector SHALL create a personal notification for that person's linked user with event type "schedule.cross_group_conflict" and metadata containing the conflicting assignment identifiers.
5. IF a person has no LinkedUserId, THEN THE Conflict_Detector SHALL skip conflict detection for that person.
6. WHEN a ScheduleVersion is published, THE Conflict_Detector SHALL complete conflict detection for all identified persons within 30 seconds of the publish event.

### Requirement 2: Trigger on User Login

**User Story:** As a user who belongs to multiple groups, I want the system to check for conflicts when I log in, so that I see any new conflicts that appeared since my last session.

#### Acceptance Criteria

1. WHEN a user successfully authenticates, THE Conflict_Detector SHALL find all Person records linked to that user's LinkedUserId across all spaces.
2. IF no Person records are found for the authenticated user's LinkedUserId, THEN THE Conflict_Detector SHALL skip conflict detection and complete without creating any notification.
3. WHEN a user successfully authenticates and Person records are found, THE Conflict_Detector SHALL check for Overlap_Conflicts and Rest_Violations across all published assignments for those Person records whose TaskSlot EndsAt is in the future (relative to the authentication timestamp).
4. WHEN the Conflict_Detector identifies one or more conflicts, THE Conflict_Detector SHALL create a personal notification for the authenticated user in each space where at least one of the conflicting assignments belongs.
5. THE Conflict_Detector SHALL execute the login-triggered check asynchronously so that the login API response time is not increased by more than 50 milliseconds compared to a login without the conflict check.

### Requirement 3: Overlap Conflict Detection

**User Story:** As a user, I want to be notified when I am double-booked at the same time in different groups, so that I can ask a manager to resolve it.

#### Acceptance Criteria

1. THE Conflict_Detector SHALL classify two assignments as an Overlap_Conflict when their TaskSlot time ranges intersect (one TaskSlot's StartsAt is before the other TaskSlot's EndsAt AND the other TaskSlot's StartsAt is before the first TaskSlot's EndsAt).
2. THE Conflict_Detector SHALL only compare assignments belonging to the same person across different groups — assignments within the same group are not cross-group conflicts.
3. THE Conflict_Detector SHALL only consider assignments from published schedule versions (status = Published).
4. WHEN the Conflict_Detector identifies one or more Overlap_Conflicts for a person, THE System SHALL create a Notification for that person containing the conflicting group names, task names, and the overlapping time range.
5. WHEN a schedule version is published, THE Conflict_Detector SHALL run overlap detection within 30 seconds for all persons who have assignments in the newly published version.
6. IF the Conflict_Detector finds no Overlap_Conflicts for a person after a schedule version is published, THEN THE System SHALL not create any conflict notification for that person.

### Requirement 4: Rest Violation Detection

**User Story:** As a user, I want to be warned when shifts in different groups are too close together, so that I have adequate rest between duties.

#### Acceptance Criteria

1. THE Conflict_Detector SHALL classify two assignments belonging to the same person from different groups as a Rest_Violation when both assignments are from published schedule versions (status = Published) and the gap between the earlier TaskSlot's EndsAt and the later TaskSlot's StartsAt is less than the applicable MinRestBetweenShiftsHours.
2. WHEN two assignments belong to groups with different MinRestBetweenShiftsHours values, THE Conflict_Detector SHALL use the larger of the two values as the required rest gap.
3. THE Conflict_Detector SHALL not flag a Rest_Violation for assignment pairs that already qualify as an Overlap_Conflict.
4. IF both groups involved in a comparison have MinRestBetweenShiftsHours set to 0, THEN THE Conflict_Detector SHALL not flag a Rest_Violation for that assignment pair.

### Requirement 5: Personal Notification

**User Story:** As a user with scheduling conflicts, I want to receive a clear personal notification so that I know which shifts conflict and can contact the relevant manager.

#### Acceptance Criteria

1. WHEN the Conflict_Detector creates a conflict notification, THE Conflict_Detector SHALL use the event type "schedule.cross_group_conflict".
2. WHEN the Conflict_Detector creates a conflict notification, THE Conflict_Detector SHALL include a localized title and body based on the space's locale setting (he, en, ru), using "en" as the default locale if the space's locale is not one of the supported values.
3. WHEN the locale is "he", THE Conflict_Detector SHALL use the title "התנגשות שיבוצים" and the body text "יש לך חפיפה בין שיבוצים — עדכן את המנהל".
4. WHEN the locale is "en", THE Conflict_Detector SHALL use the title "Schedule Conflict" and the body text "You have overlapping assignments — notify your manager".
5. WHEN the locale is "ru", THE Conflict_Detector SHALL use the title "Конфликт смен" and the body text "У вас пересечение смен — сообщите менеджеру".
6. WHEN the Conflict_Detector creates a conflict notification, THE Conflict_Detector SHALL include metadata containing the list of conflicting assignment pairs, where each pair includes: both TaskSlot IDs, both group names, and the StartsAt and EndsAt timestamps of each TaskSlot.
7. WHEN the Conflict_Detector creates a conflict notification and the user has at least one active push subscription, THE Conflict_Detector SHALL send a push notification via the existing web-push infrastructure in addition to the in-app notification.
8. IF push notification delivery fails, THEN THE Conflict_Detector SHALL retain the in-app notification without retrying the push delivery.

### Requirement 6: Privacy and Group Isolation

**User Story:** As a group manager, I want assurance that my group's schedule data is never exposed to other groups through the conflict detection mechanism.

#### Acceptance Criteria

1. THE Conflict_Detector SHALL only query assignments where the person_id matches the individual user being checked — no query shall retrieve assignments belonging to other persons within the same or other groups.
2. THE Conflict_Detector SHALL limit notification metadata to the fields defined in Requirement 5 criterion 6 (TaskSlot IDs, group names the user belongs to, and time ranges of the user's own assignments) and SHALL not include person names, assignment counts, or schedule structure of any group.
3. THE Conflict_Detector SHALL not expose conflict information to any user other than the affected individual — no API response, in-app notification, or push payload shall deliver conflict data to group managers, space owners, or other members.
4. THE Conflict_Detector SHALL not modify any existing RLS policies or tenant isolation mechanisms.
5. WHEN the Conflict_Detector resolves conflicts across spaces via LinkedUserId, THE Conflict_Detector SHALL not include group names or assignment details from Space B in any notification delivered within Space A — each space's notification SHALL reference only the group names and times visible within that space.
6. THE Conflict_Detector SHALL not provide any API endpoint or query path that allows a user to retrieve conflict information for another user.

### Requirement 7: No Solver Modification

**User Story:** As a system maintainer, I want the conflict detection to operate independently of the solver, so that per-group scheduling logic remains unchanged.

#### Acceptance Criteria

1. THE Conflict_Detector SHALL execute only after a ScheduleVersion reaches Published status — it SHALL not execute during or before the solver run that produces assignments.
2. THE Conflict_Detector SHALL not modify, block, or reject any assignment or schedule version.
3. THE Conflict_Detector SHALL not add any input or output to the solver payload (SolverInputDto).
4. IF the Conflict_Detector is unavailable or throws an error, THEN THE solver SHALL continue to produce and store assignments without interruption.
5. THE Conflict_Detector SHALL not write to any database table that the solver reads as input (including but not limited to availability_windows, constraint_rules, fairness_counters, task_slots, and assignments).

### Requirement 8: Idempotent Notification

**User Story:** As a user, I want to avoid receiving duplicate conflict notifications for the same set of conflicts, so that my notification feed remains useful.

#### Acceptance Criteria

1. WHEN the Conflict_Detector detects conflicts for a user, THE Conflict_Detector SHALL compute a deduplication fingerprint by sorting the set of conflicting assignment pair IDs (both assignment IDs in each pair, ordered lowest-first) and producing a stable hash of the sorted list.
2. IF an unread notification with event type "schedule.cross_group_conflict" already exists for the same user in the same space and carries the same deduplication fingerprint, THEN THE Conflict_Detector SHALL not create a new notification.
3. IF the user has marked a previous conflict notification as read and the same deduplication fingerprint is detected on a subsequent trigger, THEN THE Conflict_Detector SHALL create a new notification (read notifications do not suppress future duplicates).
4. WHEN new conflicts are detected whose deduplication fingerprint differs from all existing unread conflict notifications for that user in that space, THE Conflict_Detector SHALL create a new notification containing only the new conflict set.
5. WHEN a previously notified conflict is resolved (assignments no longer overlap), THE Conflict_Detector SHALL not retroactively remove or modify the existing notification.
