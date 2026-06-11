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

The deterministic solver is still available for automatic groups. Self-service
groups do not require hosted AI.

## Recommended Setup

1. Create or edit a group and set the scheduling mode to `SelfService`.
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

- Review rejected, cancelled, late, and admin-overridden activity.
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
- Rejecting marks the report as invalid but does not restore the original shift
  assignment automatically.
- Late reports count against the member's configured per-cycle limit unless
  rejected.

Shift change requests:

- Members may request a specific target slot or leave the target flexible.
- Admins must choose a target slot before approval.
- Approval moves the approved shift assignment to the new slot.
- Rejection leaves the original assignment unchanged.

Special leave:

- Pending leave can be cancelled by the member.
- Admin approval/rejection records the decision and note.
- Approved leave should be considered when planning future cycles and manual
  overrides.

## Customer-Hosted Use

Manual self-service is a strong fit for customer-hosted installs because it does
not depend on hosted AI:

- PostgreSQL stores the schedule, requests, waitlists, and review decisions.
- Redis is used for queues when available, with local fallback for small
  deployments.
- Push/email providers are optional and customer-controlled.
- AI can remain disabled or private; self-service scheduling still works.

For customer-hosted deployments, combine this guide with:

- [Customer-hosted deployment](CUSTOMER-HOSTED-DEPLOYMENT.md)
- [AI deployment modes](AI-DEPLOYMENT-MODES.md)

## Current Product Gaps To Track

These are not blockers for an MVP rollout, but they are worth improving before
large deployments:

- A more opinionated admin dashboard for urgent late absences, expiring waitlist
  offers, and underfilled slots.
- Browser end-to-end tests that exercise the complete member/admin self-service
  cycle.
- Organization-level defaults for self-service policy so new groups inherit the
  right limits.
- Better onboarding copy inside the self-service admin tabs.
- Exportable cycle report for attendance, late reports, overrides, and rejected
  requests.
