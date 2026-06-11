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
- Adds `spaceSpecialDays` web API helpers and EN/HE/RU locale strings.
- Integrates holiday/special-day weighting into solver payloads and solver
  home-leave behavior.
- Keeps the verified manual self-service special-leave notification/audit flow
  from `feat/manual-self-service-hardening`.

## Verification

- `dotnet build Jobuler.sln` passed.
- Targeted API tests passed: 206 passed, 0 failed.
- `node_modules\\.bin\\next.cmd build` from `apps/web` passed.
- `git diff --check --cached` passed before commit.

Solver Python tests were not run locally because this Windows Python
environment does not have `pytest` installed.

## Branch Relationship

Open after the manual self-service PR:

1. `feat/manual-self-service-hardening`
2. `feat/self-service-holiday-integration`

PR URL:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-holiday-integration
