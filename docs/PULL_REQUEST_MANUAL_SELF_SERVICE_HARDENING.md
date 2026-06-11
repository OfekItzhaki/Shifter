# PR: Manual Self-Service Scheduling Hardening

## Title

Harden manual self-service scheduling for customer-hosted MVP

## Description

This PR prepares manual self-service scheduling for a controlled customer-hosted
MVP. It expands the member/admin workflows around shift picking, waitlists,
cancellations, cannot-attend reports, shift changes, swaps, special leave,
attendance, closeout, provider health, and no-hosted-AI operation.

The branch also documents how this work should be integrated with the parallel
holiday-calendar and portable-space-isolation branches.

## Highlights

- Adds and hardens self-service member workflows:
  - pick shifts
  - join and leave waitlists
  - cancel owned shifts
  - report cannot attend
  - request shift changes
  - propose, accept, decline, and cancel swaps
  - submit, cancel, and track special leave
- Adds and hardens admin workflows:
  - review absence reports
  - review shift-change requests
  - review special leave
  - assign/remove members manually
  - track operations status and closeout
  - mark attendance outcomes
- Adds customer-hosted readiness:
  - Resend email provider support
  - AI deployment modes for hosted, customer-managed, or no-hosted-AI installs
  - provider health visibility for AI, email, push, solver, PostgreSQL, and Redis
  - deployment docs and customer environment validation
- Expands browser coverage for the self-service demo group:
  - picking and waitlist entry
  - waitlist leave
  - absence approval/rejection
  - shift cancellation
  - shift-change approval/rejection
  - swap acceptance/decline/cancellation
  - special leave approval/rejection/cancellation

## Verification

Recent branch verification included:

- `npm run build`
- `npm run lint`
- `npx playwright test e2e/self-service.browser.spec.ts --list`
- targeted backend tests for changed self-service behavior

Note: frontend lint currently passes with the existing warning backlog.

## Branch Relationship

Keep this PR separate from:

- `feat/holiday-calendars`
- `feat/portable-space-isolation`

Holiday calendars are adjacent scheduling semantics and should be integrated
after this branch. Portable isolation is customer-hosted infrastructure and
tenant-boundary work; it must be reconciled carefully because its current diff
conflicts with special leave and other self-service review data.

See [Self-service integration plan](SELF-SERVICE-INTEGRATION-PLAN.md).
