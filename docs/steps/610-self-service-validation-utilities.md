# Step 610 — Self-Service Validation Utilities

## Phase

Self-Service Scheduling UI — Foundation Layer

## Purpose

Provides client-side validation functions for self-service scheduling forms. These utilities run before API calls to give instant feedback on invalid inputs, preventing unnecessary network requests and improving UX.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/utils/selfServiceValidation.ts` | Three validation functions: `validateTemplateTimeRange`, `validateSelfServiceConfig`, `validateCancellationReason` |
| `apps/web/__tests__/selfService/validation.test.ts` | 32 unit tests covering all validation rules, boundary values, and edge cases |

## Key decisions

- **String comparison for time ranges**: Since times are in "HH:mm" or "HH:mm:ss" format, lexicographic comparison (`>=`) correctly determines ordering without parsing.
- **Trim before length check on cancellation reason**: Whitespace-only strings are treated as empty, and surrounding whitespace doesn't count toward the 500-char limit.
- **i18n error keys**: Each validation failure returns a specific error key under the `selfService.errors.*` namespace for localized display.
- **Validation order in config**: Fields are validated individually first (range checks), then relational constraints (min <= max) are checked. This ensures the first error found is the most specific.

## How it connects

- Used by `SelfServiceConfigTab` (task 11.1) to validate config before calling `updateSelfServiceConfig`
- Used by `ShiftTemplatesTab` (task 10.1) to validate time ranges before calling `createShiftTemplate`
- Used by `MyShiftsTab` (task 6.1) to validate cancellation reasons before calling `cancelShiftRequest`
- Error keys map to i18n messages defined in task 3.1

## How to run / verify

```bash
cd apps/web
npx vitest --run __tests__/selfService/validation.test.ts
```

All 32 tests should pass.

## What comes next

- Task 2.2: Property-based tests for validation functions (Properties 1 and 2)
- Task 3.1: i18n message keys that match the `errorKey` values returned by these functions

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): add client-side validation utilities for self-service scheduling"
```
