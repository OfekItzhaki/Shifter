# Self-Service Portability Contract

This contract defines what customer-hosted and portable-space-isolation work
must preserve for manual self-service scheduling.

Use it when merging `feat/portable-space-isolation` or building customer-owned
exports/imports. The goal is simple: a customer should be able to move a space
or organization without losing the live operational state that members and
managers see in self-service scheduling.

## Required Data

Portable exports must include these self-service records:

- `SpaceSelfServiceDefaults`: space-level defaults for request windows, absence
  limits, waitlist offer timing, and workflow toggles.
- `SpaceSpecialDay`: holiday, weekend, and custom calendar dates that affect
  manual self-service labels, warnings, coverage expectations, and closeout
  context.
- `SelfServiceConfig`: group-level self-service settings and overrides.
- `ShiftTemplate`, `ShiftSlot`, `SchedulingCycle`, and `ShiftRequest`: the
  planning structure and member assignments that self-service operates on.
- `WaitlistEntry`: waiting, offered, accepted, declined, expired, and removed
  waitlist state.
- `ShiftChangeRequest`: pending, approved, rejected, and cancelled shift-change
  requests.
- `SwapRequest`: pending, accepted, declined, cancelled, and expired swap
  requests.
- `ShiftAttendanceRecord`: present, no-show, excused, notes, and reviewer
  metadata.
- `SpecialLeaveRequest`: pending, approved, rejected, and cancelled leave
  requests, including the presence-window linkage created on approval.
- Notifications and audit records that reference these workflows when they are
  included in the same portability package.

## Scope Rules

Every exported record must stay inside the selected organization or space
boundary.

- Preserve `SpaceId` and `GroupId` relationships.
- Preserve member/person relationships for request owners, assignees, targets,
  reviewers, and waitlist participants.
- Preserve current workflow status values exactly; do not collapse historical
  rows to a summary.
- Preserve timestamps used for cutoffs, waitlist offer expiry, attendance, and
  closeout reporting.
- Preserve notes and admin review comments unless a customer explicitly asks for
  a redacted export.

## Import Rules

Imports must rebuild a usable self-service state, not only seed configuration.

- Import defaults before group config.
- Import templates, cycles, slots, and shift requests before review queues.
- Import waitlists, changes, swaps, attendance, and special leave after their
  referenced slots, requests, people, groups, and spaces exist.
- Re-map identifiers consistently across related rows.
- Do not trigger member notifications while importing historical state.
- Do not run waitlist expiry or swap expiry jobs until the import transaction is
  complete.

## Tenant Isolation Checks

Portable isolation must prove these member/admin surfaces cannot cross tenant,
organization, space, or group boundaries:

- self-service config and space defaults
- shift picking, cancellation, and cannot-attend reporting
- waitlist join, leave, accept, expiry, and admin views
- shift-change submit, cancel, approve, and reject
- swap propose, accept, decline, cancel, expiry, and admin views
- attendance recording and slot visibility
- special leave submit, cancel, approve, reject, and admin views
- closeout CSV/PDF export and status counters

## Merge Gate

Before merging portable isolation after manual self-service:

1. Confirm `SpecialLeaveRequestsController`, special leave commands, queries,
   DTOs, domain entity, EF configuration, and migrations are still present.
2. Confirm export/import tests include every record type listed above.
3. Confirm tenant isolation tests cover both member and admin self-service
   endpoints.
4. Confirm closeout metrics survive export/import with matching counts.
5. Run the manual self-service QA checklist after the merge.
