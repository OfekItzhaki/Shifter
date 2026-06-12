# Manual Self-Service Scheduling

This guide describes how to run Shifter when members pick, manage, and trade
their own shifts instead of relying on the solver to assign every slot.

Use this mode for organizations that want a controlled self-service workflow:
admins define the rules and review exceptions, while members handle day-to-day
shift selection from the web app or PWA.

## What This Mode Covers

Manual self-service currently supports:

- Member shift requests for open slots.
- Waitlists for full slots, including timed offers.
- Member cancellation before the configured cutoff.
- "Cannot attend" reports after a member already owns a shift.
- Per-cycle late absence limits.
- Shift change requests, either to a specific slot or a flexible admin-selected
  replacement.
- Shift swaps between members.
- Special leave requests.
- Admin assignment and removal overrides.
- Admin review queues for absences, shift changes, and special leave.
- Cycle closeout summary for coverage, unresolved items, overrides, absences,
  swaps, waitlist state, special leave, and special-day impact.
- Admin-confirmed attendance outcomes for approved shifts: present, no-show, or
  excused.
- Special-day policy for closures or no-coverage dates: slots on marked special
  days with `requiresCoverage=false` stay visible with a no-coverage label, but
  members cannot pick them or join/accept waitlists for them.
- Special leave validation against special days: leave requests overlapping
  `requiresCoverage=false` dates are rejected, while coverage-required special
  days remain requestable and are highlighted to admins.

The deterministic solver is still available for automatic groups. Self-service
groups do not require hosted AI.

## MVP Completeness Checklist

The current manual self-service implementation is suitable for a controlled MVP
when the organization accepts an admin-operated review queue:

| Need | Current support |
|---|---|
| Members choose shifts | Supported through available slots, with duplicate, overlap, rest-window, capacity, max-shift, and no-coverage special-day checks. |
| Full slots stay useful | Supported through waitlists and timed offers. |
| Members cannot attend | Supported through absence reports, with late-report counting per cycle. |
| Members ask to change shifts | Supported for specific target slots and flexible admin-selected targets. |
| Members swap shifts | Supported through member-to-member swap requests with ownership and schedule-safety checks. |
| Members request planned time off | Supported through special leave requests and admin review, with special-day overlap checks. |
| Admin fills gaps manually | Supported through admin assignment/removal overrides. Overrides can exceed capacity when needed, but still reject started/closed slots, duplicate assignments, overlap conflicts, and rest-window violations. |
| Admin sees the operating state | Supported through operations status, prioritized underfilled-slot gaps, closeout summary, attendance/no-show counts, review counts, waitlist/admin queues, and manual assignment tools. |
| Customer-hosted/no-AI use | Supported. Manual self-service does not require hosted AI. |

This is not yet a fully autonomous scheduling product. Admins still need to
review exceptions, close gaps, and tune policy between cycles.

## Current Implementation Map

Use this section when checking whether a customer-facing workflow is actually
present in the product.

| Workflow | Member surface | Admin surface | Backend support |
|---|---|---|---|
| Pick shifts | `Available slots` tab and `/pick` mobile route | Operations status and admin overrides | `ShiftSlotsController`, `ShiftRequestsController`, `ShiftRequestService`, `SlotAvailabilityEngine` |
| Waitlist full shifts | `Waitlist` tab, offer accept/decline, leave waitlist | Admin waitlist queue and manual assignment from waitlist | `WaitlistService`, waitlist endpoints, expired-offer job, no-coverage special-day checks |
| Cancel owned shifts | `My shifts` cancel action before cutoff | Cancelled request visibility in review history | shift request cancellation flow with cutoff checks |
| Report cannot attend | `My shifts` cannot-attend action | `Reviews` queue for absence approval/rejection | `ShiftAbsenceReport` flow with late-report counting |
| Ask to change shift | `My shifts` change request, specific slot or flexible | `Reviews` queue with target slot selection | `ShiftChangeRequestsController` and change request domain model |
| Swap shifts | `Swaps` tab, propose/accept/decline/cancel | Visible through member/admin group tabs | `ShiftSwapService`, swap expiry job, schedule-safety checks |
| Planned time off | Special leave form in `My shifts` | `Reviews` queue for special leave | special leave API and review flow |
| Fill gaps manually | Not applicable | `Admin overrides` assignment/removal | admin override commands with safety checks |
| Run cycles | Members see generated slots | Config, templates, cycle controls, operations dashboard | self-service config, templates, cycle generation jobs |
| Confirm attendance | Not applicable | Attendance mark on approved shift requests | `ShiftAttendanceRecord` and shift request attendance endpoint |
| Close out cycles | Not applicable | Closeout summary, CSV export, and PDF report in Operations, including active workflow policy flags, no-show, unconfirmed attendance, special-day slot, no-coverage special-day, and underfilled special-day counts | `SelfServiceCyclesController` closeout endpoint |

The strongest member entry point is `/pick`, especially for PWA/mobile users.
The strongest manager entry point is the self-service group operations tab.

## Recommended Setup

1. Create or edit a group and set the scheduling mode to `SelfService`.
   Shifter creates a default self-service policy record when the mode is
   enabled, so the group has concrete limits before the first cycle is opened.
   Space owners can set the default template from `Space Settings` ->
   `Self-Service`. Platform admins can also set an organization-level template
   through the platform organization self-service defaults API for multi-space
   customers. Customer-hosted deployments can also set install-level defaults with
   `SELF_SERVICE_DEFAULT_*` env vars before groups are switched to
   `SelfService`.
2. Add members, roles, qualifications, and tasks as usual.
3. Open the self-service admin area for the group.
4. Configure self-service policy:

   | Setting | Recommended starting value | Why |
   |---|---:|---|
   | Minimum shifts per cycle | `1` or `2` | Gives admins a clear under-scheduled signal. |
   | Maximum shifts per cycle | Based on team size | Prevents one member from taking too many slots. |
   | Request window open offset | `72` hours | Gives members time to choose. |
   | Request window close offset | `12` to `24` hours | Gives admins time to fix gaps. |
   | Cancellation cutoff | `24` hours | Encourages early cancellations. |
   | Late absence window | `24` hours | Defines what counts against the late limit. |
   | Max late reports per cycle | `1` or `2` | Controls repeated last-minute absence reports. |
   | Waitlist offer duration | `30` to `60` minutes | Keeps waitlists moving. |
   | Cycle duration | `7` days | Weekly cycles are easiest to operate. |

5. Create shift templates for the recurring slots the group needs.
6. Generate the next self-service cycle.
7. Open the request window.

## Admin Operating Rhythm

### Before The Cycle

- Confirm templates match the real operating need.
- Generate the next cycle and verify total capacity.
- Open the request window.
- Watch the operations tab for underfilled slots and review queue counts.
- Use the priority coverage gaps to handle the nearest underfilled slots first.

### While Members Are Picking Shifts

- Let members request slots directly from `Available slots`.
- Use the waitlist panel for full slots.
- Avoid admin overrides unless there is a real operational need.
- Check under-scheduled members after the request window closes.

### During The Live Cycle

- Review "cannot attend" reports quickly, especially late ones.
- Approve or reject shift change requests.
- Use admin assignment/removal for emergency coverage.
- Monitor waitlist offers so released slots do not stay empty.

### After The Cycle

- Review the closeout summary for coverage, unresolved requests, late reports,
  cancellations, overrides, swaps, waitlist outcomes, attendance/no-shows,
  special-day impact, active workflow policy, and special leave.
- Export the closeout CSV when the cycle needs to be archived or shared with a
  customer administrator. The export includes the member workflow toggles that
  were active for the group, so a customer can audit which self-service actions
  were allowed for the cycle.
- Clear or document any remaining underfilled slots and pending review items.
- Mark approved assignments as present, no-show, or excused once attendance is
  known.
- Adjust the next cycle policy if the group was overbooked, underfilled, or had
  too many late reports.

## Member Workflow

Members can:

- Pick available shifts.
- Join a waitlist when a slot is full.
- Accept or decline waitlist offers.
- View their current shifts and request history.
- Cancel a shift before the cutoff.
- Report that they cannot attend a future shift.
- Ask to change a shift.
- Propose swaps with another member.
- Request special leave.

Members should use normal cancellation when they are still before the cutoff.
"Cannot attend" is for cases where the shift is already owned and needs admin
visibility.

## Admin Review Rules

Absence reports:

- Approving confirms the reported absence.
- Rejecting marks the report as invalid and reinstates the original shift
  assignment when the report was the reason the shift was released.
- Late reports count against the member's configured per-cycle limit unless
  rejected.

Shift change requests:

- Members may request a specific target slot or leave the target flexible.
- Admins must choose a target slot before approval.
- Approval moves the approved shift assignment to the new slot.
- Rejection leaves the original assignment unchanged.

Special leave:

- Requests overlapping no-coverage special days are rejected because there is no
  coverage obligation to be excused from.
- Requests overlapping coverage-required special days are allowed, and admin
  notifications include the special-day context.
- Pending leave can be cancelled by the member.
- Admin approval/rejection records the decision and note.
- Approved leave should be considered when planning future cycles and manual
  overrides.

## Safety And Guardrails

Manual self-service deliberately separates normal member actions from admin
override actions:

- Members cannot claim started slots, duplicate their own pending/approved
  requests, exceed configured max shifts, or take slots that conflict with
  approved shifts or minimum rest windows.
- Members can join waitlists for full slots instead of directly overfilling a
  slot.
- Waitlist offers expire, and accepted offers are rechecked before assignment.
- Special days marked with `requiresCoverage=false` block member picks,
  waitlist joins, waitlist offer cascades, and stale waitlist offer acceptance.
  Admins can still use override tools when a real exception is needed.
- Admin assignment can exceed capacity for emergency coverage, but it still
  blocks unsafe assignment conflicts and only targets open future slots.
- Admin removal cancels the assignment, decrements fill count, and triggers
  waitlist processing when capacity opens.
- Review decisions are auditable through persisted request state and audit logs.

Operationally, admins should treat overrides as an exception path. If overrides
become frequent, adjust templates, capacity, request windows, or min/max shift
policy before the next cycle.

## Verification Evidence

The implementation has focused unit/property coverage for:

- self-service tab visibility and admin/member access shape
- slot browsing, sorting, capacity display, shift picking, and waitlist joins
- member shift status, cancellation eligibility, absence reporting, shift
  changes, special leave, and unified activity history
- waitlist offers, stale-offer handling, admin waitlist assignment, and leaving
  the waitlist
- swaps, including propose, accept, decline, cancel, expiry, ownership checks,
  conflict checks, and stale-state refresh
- self-service config validation and policy warnings
- workflow toggles that disable member shift claims, waitlists, absence
  reports, shift-change requests, and swaps
- admin overrides, absence/change/special-leave review queues, and cycle
  operations status
- cycle closeout metrics, including coverage totals, unresolved items,
  absences, changes, swaps, waitlists, attendance/no-shows, special-day impact,
  special leave, and admin overrides
- attendance record creation/update behavior and tenant-scoped closeout counts
- API lifecycle tests for request limits, notifications, waitlist processing,
  swaps, absence reports, shift changes, and scope isolation

There are also browser Playwright checks using the seeded `Self-Service Demo`
group:

- a client-ready smoke runner:
  `.\infra\scripts\smoke-self-service-client-ready.ps1`
- a mobile smoke test for self-service admin cycle controls and operations
- a member browser lifecycle test for picking an open shift and joining/viewing
  a full-slot waitlist
- a member browser lifecycle test for leaving a waiting-list entry
- a member/admin browser lifecycle test for cannot-attend reporting and admin
  approval from the review queue
- a member/admin browser lifecycle test for shift-change request submission,
  admin approval, and reassignment verification
- a member browser lifecycle test for cancelling an approved shift and verifying
  the cancelled state
- a member-to-member browser lifecycle test for proposing and accepting a shift
  swap with final assignment verification
- a member-to-member browser lifecycle test for proposing and declining a shift
  swap
- a member browser lifecycle test for cancelling an outgoing pending shift swap
- an admin browser lifecycle test for rejecting a shift-change request while
  preserving the member's original assignment
- an admin browser lifecycle test for rejecting a cannot-attend report and
  reinstating the member's released shift
- a member/admin browser lifecycle test for special leave submission and admin
  approval
- a member browser lifecycle test for special leave cancellation
- a member/admin browser lifecycle test for special leave rejection
- a member browser lifecycle test for holiday/special-day labels on available
  shifts

## Customer-Hosted Use

Manual self-service is a strong fit for customer-hosted installs because it does
not depend on hosted AI:

- PostgreSQL stores the schedule, requests, waitlists, and review decisions.
- Redis is used for queues when available, with local fallback for small
  deployments.
- Push/email providers are optional and customer-controlled.
- AI can remain disabled or private; self-service scheduling still works.
- `/health/detailed` reports provider readiness for email, push, AI, solver,
  PostgreSQL, and Redis so customer-hosted installs can verify dependencies
  before rollout.

For customer-hosted deployments, combine this guide with:

- [Customer-hosted deployment](CUSTOMER-HOSTED-DEPLOYMENT.md)
- [AI deployment modes](AI-DEPLOYMENT-MODES.md)

## Current Product Gaps To Track

These are not blockers for an MVP rollout, but they are worth improving before
large deployments:

- Broader browser end-to-end coverage for the complete member/admin cycle:
  remaining rejection paths and richer final slot-state verification. The
  browser suite already covers picking, waitlist entry, waitlist leaving,
  cannot-attend reporting, absence approval, absence rejection with shift
  reinstatement, shift cancellation, shift-change approval with reassignment,
  shift-change rejection, member-to-member swap acceptance, member-to-member
  swap decline, and initiator swap cancellation, special leave approval, special
  leave cancellation, and special leave rejection.
- Organization-level defaults now exist in the backend and platform API, and
  spaces inherit them before falling back to install defaults. A polished admin
  UI for editing those organization templates is still deferred.
- Formal certificate signing for closeout reports. PDF closeout reports already
  include a verification fingerprint for archive/tamper checks, but they are not
  certificate-signed legal documents.
