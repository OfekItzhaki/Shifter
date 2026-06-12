# PR: Self-Service Holiday Calendar Integration

## Title

Integrate holiday calendars with manual self-service scheduling

## Description

This PR stacks on top of `feat/manual-self-service-hardening` and integrates the
holiday-calendar branch into the manual self-service scheduling path without
changing the verified special-leave approval workflow.

It adds space-level special days, exposes them inside the existing space
self-service settings tab, and carries the holiday-aware solver/home-leave
payload changes forward for scheduling.

## Highlights

- Adds `SpaceSpecialDay` domain, EF configuration, migration, commands,
  queries, API controller, and tests.
- Adds `SpecialDaysCard` to space settings under the `Self-service` tab.
- Adds self-service slot labels and cycle warning counts for marked special
  days, without enforcing automatic holiday restrictions yet.
- Adds a browser lifecycle flow in the top integration branch for member picker
  special-day labels.
- Adds `spaceSpecialDays` web API helpers and EN/HE/RU locale strings.
- Integrates holiday/special-day weighting into solver payloads and solver
  home-leave behavior.
- Keeps the verified manual self-service special-leave notification/audit flow
  from `feat/manual-self-service-hardening`.

## Verification

- `dotnet build Jobuler.sln` passed.
- Targeted API tests passed: 206 passed, 0 failed.
- Holiday-aware self-service API tests passed in the top integration branch:
  71 passed, 0 failed.
- Holiday-aware self-service component tests passed in the top integration
  branch: 7 passed, 0 failed.
- Self-service browser test discovery in the top integration branch found 14
  lifecycle tests, including the special-day label flow.
- `node_modules\\.bin\\next.cmd build` from `apps/web` passed.
- Frontend lint passes on the current branch stack with 89 existing warnings
  and 0 errors.
- `git diff --check --cached` passed before commit.

Solver Python tests were not run locally because this Windows Python
environment does not have `pytest` installed.

## Branch Relationship

Open after the manual self-service PR:

1. `feat/manual-self-service-hardening`
2. `feat/self-service-holiday-integration`

PR URL:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-holiday-integration
