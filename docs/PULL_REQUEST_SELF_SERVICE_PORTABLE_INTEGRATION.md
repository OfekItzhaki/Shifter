# PR: Self-Service Portable Space Integration

## Title

Integrate portable space isolation with self-service scheduling

## Description

This PR stacks on top of the manual self-service and holiday-calendar
integration branches. It reconciles `feat/portable-space-isolation` with the
self-service scheduling work so customer-hosted/on-prem style deployments can
preserve organization, billing, contact protection, and export/import boundaries
without dropping self-service review workflows.

## Highlights

- Adds organization and organization-subscription domain/application/API support.
- Adds organization export/import validation and manifest queries.
- Adds contact lookup protection and field encryption for user contact fields.
- Adds organization portability tests and contact field protection tests.
- Includes manual self-service workflow records in organization export manifests,
  packages, and import validation counts.
- Preserves special leave controller, commands, queries, DTOs, EF/domain entity,
  and self-service workflow data.
- Keeps current landing/login/auth UI where portable conflicted with the newer
  product design.
- Reconciles portable group-tree solver scoping with holiday/self-service solver
  payload behavior.

## Verification

- `dotnet build Jobuler.sln` passed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~OrganizationPortabilityTests"`
  passed: 17 passed, 0 failed.
- `dotnet test apps\\api\\Jobuler.Tests\\Jobuler.Tests.csproj --filter "FullyQualifiedName~OrganizationPortability|FullyQualifiedName~SelfService|FullyQualifiedName~SpecialLeave|FullyQualifiedName~SpaceSpecialDay"`
  passed: 204 passed, 0 failed.
- Focused API tests passed: 223 passed, 0 failed.
- Full API suite passed: 1,947 passed, 12 skipped, 0 failed.
- `node_modules\\.bin\\next.cmd build` from `apps/web` passed.
- `node_modules\\.bin\\eslint.cmd .` from `apps/web` passed with 89 existing
  warnings and 0 errors.
- `node_modules\\.bin\\playwright.cmd test e2e/self-service.browser.spec.ts --list`
  from `apps/web` discovered 13 self-service browser lifecycle tests.
- `git diff --check --cached` passed before commit.

## Integration Notes

Portable isolation should be reviewed after:

1. `feat/manual-self-service-hardening`
2. `feat/self-service-holiday-integration`
3. `feat/self-service-portable-integration`

Special leave files were explicitly checked and remain tracked:

- `SpecialLeaveRequestsController`
- `SpecialLeaveRequestCommands`
- `SpecialLeaveRequestQueries`
- `SpecialLeaveDtos`
- `SpecialLeaveRequest`

PR URL:

https://github.com/OfekItzhaki/Shifter/pull/new/feat/self-service-portable-integration
