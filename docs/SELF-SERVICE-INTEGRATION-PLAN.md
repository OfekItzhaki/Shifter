# Self-Service Integration Plan

This note keeps the current branch stack clear while manual self-service,
holiday calendars, and customer-hosted portability are developed in parallel.

## Current Branch

`feat/manual-self-service-hardening` is the manual self-service branch.

It should stay focused on:

- member shift picking, waitlists, cancellations, absence reports, shift changes,
  swaps, special leave, attendance, and closeout
- admin review queues and manual overrides
- customer-hosted/no-hosted-AI operation for self-service groups
- browser/API coverage proving the member and admin workflows

Do not merge unrelated organization portability or holiday-calendar schema
changes into this branch unless they are required to keep self-service compiling.

## Holiday Calendars

`feat/holiday-calendars` is adjacent to self-service scheduling, not a
replacement for it.

Holiday and special-day support should eventually feed self-service policy and
slot planning because holidays can change:

- expected staffing demand
- request windows and cutoff rules
- member availability or leave expectations
- solver inputs for automatic groups
- generated self-service cycle templates

Recommended sequence:

1. Merge or reconcile `feat/manual-self-service-hardening` first.
2. Rebase or recreate holiday-calendar work on top of the self-service branch.
3. Add explicit self-service behavior for holidays only after the holiday data
   model is stable.
4. Add tests proving holiday dates affect self-service cycle generation or
   policy warnings before exposing it as a customer-facing scheduling feature.

Known reconciliation points from the branch diff:

- `SpaceSpecialDaysController` and `SpecialDaysCard` should be kept if the
  feature is still wanted.
- `SpecialLeaveRequestsController` is touched by the holiday branch, so special
  leave submit/approve/reject/cancel browser coverage should be rerun after the
  merge.
- The holiday branch adds solver/special-day inputs. Confirm those inputs do not
  change self-service groups unless self-service holiday behavior is explicitly
  implemented.
- Add at least one self-service test for a special day before presenting holiday
  calendars as part of manual scheduling.

Use the
[self-service holiday calendar contract](SELF-SERVICE-HOLIDAY-CALENDAR-CONTRACT.md)
as the merge gate for product behavior, policy, data scope, and tests.

## Portable Space Isolation

`feat/portable-space-isolation` is related to customer-hosted deployments and
organization boundaries, but it is not the same feature as manual self-service.

That branch appears to contain organization export/import, organization billing,
tenant isolation, and contact-field protection work. It also has a wide diff
against `main`, so it should not be merged casually into self-service.

Recommended sequence:

1. Review portable isolation against the current self-service data model.
2. Preserve self-service review entities: absence reports, shift change
   requests, waitlists, swaps, attendance, special leave, and closeout data.
3. Verify tenant scoping/RLS for every self-service table before using it for
   customer-hosted installs.
4. Re-run customer-hosted health checks and export/import tests after merging.

Known reconciliation points from the branch diff:

- The branch deletes `SpecialLeaveRequestsController`,
  `SpecialLeaveRequestCommands`, `SpecialLeaveRequestQueries`,
  `SpecialLeaveRequestDto`, and the `SpecialLeaveRequest` domain entity when
  compared with `main`. That conflicts with the current manual self-service
  branch, where special leave is a supported workflow and has browser coverage.
- Organization export/import must include all self-service workflow data:
  shift requests, absence reports, shift change requests, waitlist entries, swap
  requests, attendance records, special leave requests, self-service defaults,
  templates, slots, cycles, and closeout artifacts.
- Tenant isolation must be verified for every self-service admin and member
  endpoint, not only for spaces and organizations.
- Billing changes should preserve the current space-level self-service billing
  behavior until organization-level billing is explicitly rolled out.

Use the [self-service portability contract](SELF-SERVICE-PORTABILITY-CONTRACT.md)
as the merge gate for export/import, tenant isolation, and special leave
preservation.

## Practical Merge Order

Use this order unless a production fix forces a different path:

1. `feat/manual-self-service-hardening`
2. `feat/holiday-calendars`
3. `feat/portable-space-isolation`

The reason is dependency shape: self-service defines the workflow and data that
customers will operate; holidays add calendar semantics to that workflow; portable
isolation packages the final system for customer-owned infrastructure.
