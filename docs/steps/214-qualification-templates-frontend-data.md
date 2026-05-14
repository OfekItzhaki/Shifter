# 214 — Qualification Templates Frontend Data & API Client

## Phase

Phase 5 — Qualification Templates & Unavailability Reasons (Frontend)

## Purpose

Extends the frontend group template data with qualification and unavailability reason definitions, creates the API client for unavailability reason CRUD, and verifies the solver normalizer correctly ignores the new `UnavailabilityReasonId` field on presence windows.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/utils/groupTemplates.ts` | Extended `GroupTemplate` interface with `qualifications` and `unavailabilityReasons` fields; populated all industry templates with their respective data |
| `apps/web/lib/api/unavailabilityReasons.ts` | New API client with `getReasons`, `createReason`, `updateReason`, `deleteReason`, and `seedReasons` functions |
| `apps/api/Jobuler.Tests/Scheduling/SolverPayloadNormalizerTests.cs` | Added two unit tests verifying solver DTO ignores `UnavailabilityReasonId` |

## Key decisions

- **Template data is frontend-only**: Qualification and reason lists live in the TypeScript template file. The frontend sends individual create requests during template application, keeping the backend stateless about template definitions.
- **Shared reasons across templates**: All industry templates use the same 4 Hebrew unavailability reasons (חופשה, מחלה, אישי, לימודים). Custom template gets empty arrays.
- **Solver verification via reflection**: Added a test that uses reflection to assert the `PresenceWindowDto` record type only has 4 fields (PersonId, State, StartsAt, EndsAt), ensuring no one accidentally adds reason data to the solver payload.

## How it connects

- Task 5.1 provides the data that tasks 5.2 and 5.3 will use when wiring the template picker to create qualifications and seed reasons.
- Task 6.1 provides the API client that task 6.2 (settings panel) and task 5.3 (seed on template apply) will use.
- Task 8.1 confirms the solver is unaffected by the new `UnavailabilityReasonId` field added in task 1.2.

## How to run / verify

```bash
# TypeScript type-checking
cd apps/web && npx tsc --noEmit

# .NET tests for solver normalizer
dotnet test apps/api/Jobuler.Tests --filter "SolverPayloadNormalizerTests.PresenceWindows" --verbosity normal
dotnet test apps/api/Jobuler.Tests --filter "SolverPayloadNormalizerTests.SolverPresenceWindowDto" --verbosity normal
```

## What comes next

- Task 5.2: Wire `GroupTemplatePicker` to create qualifications on template apply
- Task 5.3: Wire `GroupTemplatePicker` to seed unavailability reasons on template apply
- Task 6.2: Create the Unavailability Reasons settings panel component

## Git commit

```bash
git add -A && git commit -m "feat(phase5): extend templates with qualifications/reasons, add unavailability API client, verify solver compatibility"
```
