# PR: Self-Service Portability Export Readiness

## Title

Export self-service workflow data and show holiday-aware cycle context

## Description

This PR is a top integration branch on the manual self-service, holiday-calendar,
and portable-isolation stack. It closes the portability export gap for manual
self-service workflow data and adds the first customer-facing holiday-awareness
slice inside manual self-service scheduling.

## Highlights

- Includes self-service defaults, configs, cycles, templates, slots, requests,
  attendance, absences, changes, waitlists, swaps, special leave, and special
  days in organization export manifests/packages and import validation counts.
- Adds regression coverage proving organization exports remain scoped while
  preserving manual self-service workflow records.
- Adds special-day metadata to available self-service slots.
- Shows special-day badges on member slot cards.
- Adds special-day counts to admin cycle status and labels underfilled slots
  that fall on marked special days.
- Adds a Playwright browser lifecycle flow that creates a special day for a
  real seeded self-service slot and verifies the member picker shows it.
- Keeps this as visible awareness only; holiday-specific workflow restrictions
  remain a later policy feature.

## Verification

- `dotnet build apps\\api\\Jobuler.sln` passed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~OrganizationPortabilityTests"`
  passed: 17 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~OrganizationPortability|FullyQualifiedName~SelfService|FullyQualifiedName~SpecialLeave|FullyQualifiedName~SpaceSpecialDay"`
  passed: 204 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~SlotAvailabilityEngineTests|FullyQualifiedName~SelfServiceScopeTests"`
  passed: 71 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.sln` passed: 1,949 passed, 12 skipped,
  0 failed.
- `node_modules\\.bin\\next.cmd build` from `apps/web` passed.
- `node_modules\\.bin\\eslint.cmd .` from `apps/web` passed with 89 existing
  warnings and 0 errors.
- `node_modules\\.bin\\vitest.cmd run __tests__\\selfService\\slotBrowserTab.test.tsx __tests__\\selfService\\cycleControlPanel.test.tsx`
  passed: 7 passed, 0 failed.
- `node_modules\\.bin\\eslint.cmd e2e\\self-service.browser.spec.ts` passed.
- `node_modules\\.bin\\playwright.cmd test self-service.browser.spec.ts --list`
  discovered 14 browser lifecycle tests, including the new special-day label
  flow.

## Remaining Product Checks

- Run the special-day browser lifecycle flow against a live seeded web/API stack.
- Smoke-test customer-hosted setup with real secrets and a real database,
  especially `FIELD_ENCRYPTION_KEY`.

PR URL:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-portability-export-readiness
