# Self-Service Branch Stack Status

Use this as the current branch-stack map for manual self-service,
holiday-calendar integration, and customer-hosted portability.

## Merge Order

Merge and test in this order:

1. `feat/manual-self-service-hardening`
2. `feat/self-service-holiday-integration`
3. `feat/self-service-portable-integration`
4. `feat/self-service-portability-export-readiness`

The order matters because manual self-service defines the member/admin workflow,
holiday calendars add scheduling semantics, and portable isolation packages the
result for customer-owned infrastructure. The final export-readiness branch
keeps a separate PR boundary for self-service export coverage and the first
holiday-aware manual self-service labels/warnings.

## Branches

### `feat/manual-self-service-hardening`

Purpose: harden manual self-service scheduling for a customer-hosted MVP.

PR:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/manual-self-service-hardening

PR summary:

[Manual self-service PR summary](PULL_REQUEST_MANUAL_SELF_SERVICE_HARDENING.md)

Known verification:

- Frontend build passed.
- Frontend lint passed with existing warnings and 0 errors.
- Self-service browser test discovery found 13 lifecycle tests.
- Targeted backend self-service tests passed.
- Full API suite passed: 1,921 passed, 12 skipped, 0 failed.

### `feat/self-service-holiday-integration`

Purpose: integrate holiday/special-day support on top of manual self-service.

PR:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-holiday-integration

PR summary:

[Holiday integration PR summary](PULL_REQUEST_SELF_SERVICE_HOLIDAY_INTEGRATION.md)

Known verification:

- API build passed.
- Targeted API tests passed: 206 passed, 0 failed.
- Holiday-aware self-service API tests passed in the top integration branch:
  71 passed, 0 failed.
- Holiday-aware self-service component tests passed in the top integration
  branch: 7 passed, 0 failed.
- Web build passed.
- Frontend lint passes on the current stack with existing warnings and 0 errors.

Validated integration:

- Confirm special days appear under the space `Self-service` settings tab.
- Confirm the first customer-facing version only labels/warns for holidays until
  explicit self-service holiday policies are implemented.

Remaining manual/product check:

- Add a full holiday-aware self-service browser flow before claiming full
  customer-facing holiday policy support.

### `feat/self-service-portable-integration`

Purpose: integrate organization/tenant portability, contact protection, and
export/import boundaries on top of self-service and holiday calendars.

PR:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-portable-integration

PR summary:

[Portable integration PR summary](PULL_REQUEST_SELF_SERVICE_PORTABLE_INTEGRATION.md)

Known verification:

- API build passed.
- Organization portability tests passed: 17 passed, 0 failed.
- Focused portability/self-service/special-day tests passed: 204 passed, 0 failed.
- Focused API tests passed: 223 passed, 0 failed.
- Full API suite passed: 1,947 passed, 12 skipped, 0 failed.
- Web build passed.
- Frontend lint passed with 89 existing warnings and 0 errors.
- Self-service browser test discovery found 13 lifecycle tests.

Preserved self-service files:

- `SpecialLeaveRequestsController`
- `SpecialLeaveRequestCommands`
- `SpecialLeaveRequestQueries`
- `SpecialLeaveDtos`
- `SpecialLeaveRequest`

Validated integration:

- Organization export manifests, packages, and import validation counts now
  include manual self-service workflow records listed in the portability contract.

Remaining manual/product check:

- Smoke-test customer-hosted setup with real secrets, especially
  `FIELD_ENCRYPTION_KEY`.
- Smoke-test member/admin self-service flows after migrations run on a real
  database.

### `feat/self-service-portability-export-readiness`

Purpose: top integration branch for export-ready manual self-service state and
holiday-aware self-service cycle context.

PR:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-portability-export-readiness

PR summary:

[Portability export readiness PR summary](PULL_REQUEST_SELF_SERVICE_PORTABILITY_EXPORT_READINESS.md)

Known verification:

- API build passed.
- Organization portability tests passed: 17 passed, 0 failed.
- Focused portability/self-service/special-day tests passed: 204 passed, 0 failed.
- Focused slot/status holiday-awareness tests passed: 71 passed, 0 failed.
- Full API suite passed: 1,949 passed, 12 skipped, 0 failed.
- Web build passed.
- Frontend lint passed with 89 existing warnings and 0 errors.
- Holiday-aware self-service component tests passed: 7 passed, 0 failed.

Validated integration:

- Organization export manifests, packages, and import validation counts include
  manual self-service workflow records.
- Member-facing self-service slots show marked special days.
- Admin cycle status exposes special-day counts and labels underfilled slots on
  marked special days.

Remaining manual/product check:

- Add a full holiday-aware self-service browser flow before claiming full
  customer-facing holiday policy support.
- Smoke-test customer-hosted setup with real secrets, especially
  `FIELD_ENCRYPTION_KEY`.
- Smoke-test member/admin self-service flows after migrations run on a real
  database.

## PR Opening Notes

GitHub CLI is not installed in this local environment, so open the PRs using the
URLs above.

When creating the PRs, use each linked PR summary file for the title and
description. Keep the PR bases stacked in the same order as this document.
