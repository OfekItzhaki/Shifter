# Step 612 — Self-Service Error Code Mapping Utility

## Phase

Phase — Self-Service Scheduling UI (Frontend)

## Purpose

Provides a centralized utility for mapping backend ProblemDetails error type slugs to Hebrew i18n keys in the self-service scheduling module. This ensures consistent, localized error messages across all self-service UI components.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/utils/selfServiceErrors.ts` | Error code mapping utility with `getSelfServiceErrorMessage()` and `getErrorI18nKey()` functions |
| `apps/web/__tests__/selfService/errorMapping.test.ts` | Unit tests covering all error mapping scenarios (14 tests) |

## Key decisions

1. **422 ProblemDetails detail displayed directly** — The backend already provides human-readable Hebrew messages in the `detail` field for 422 domain validation errors. Displaying these directly avoids maintaining duplicate translations.
2. **Dual return format** — `getSelfServiceErrorMessage()` returns both the message and a flag indicating whether it's an i18n key or a direct string. This lets components decide whether to pass through `t()` or display directly.
3. **Simple error format fallback** — Some controllers (ShiftSwaps, AdminOverrides) return `{ error: "..." }` instead of ProblemDetails. The utility handles both formats.
4. **Type slug extraction from URI** — The ProblemDetails `type` field is a full URI; the utility extracts the last segment as the slug for mapping.

## How it connects

- Used by all self-service tab components (SlotBrowser, MyShifts, Waitlist, Swaps, AdminOverrides) for error display
- Maps to i18n keys defined in `apps/web/messages/he.json` under `selfService.errors.*` (added in task 3.1)
- Follows the same error extraction pattern used throughout the codebase (`err?.response?.data?.error`)
- Integrates with the backend's ProblemDetails format from `ExceptionHandlingMiddleware`

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/selfService/errorMapping.test.ts
```

All 14 tests should pass.

## What comes next

- Task 3.3: Property-based test for error code mapping (Property 14)
- Self-service tab components will import `getSelfServiceErrorMessage` for error display

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): error code mapping utility with i18n key resolution"
```
