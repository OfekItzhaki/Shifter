# Step 597 — ShiftRequestsController Problem Details Migration

## Phase

Phase: Problem Details Migration — Controller-level error responses

## Purpose

Migrate the `ShiftRequestsController` error responses from custom JSON shapes (`ShiftRequestErrorResponse` record and anonymous objects) to RFC 7807 ProblemDetails format using the `ProblemDetailsResults.Problem()` helper. This ensures consistent error response structure across the API.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/ShiftRequestsController.cs` | Replaced `UnprocessableEntity(new ShiftRequestErrorResponse(...))` in `Submit` with `ProblemDetailsResults.Problem()` call (422, "shift-request-rejected", with `alternativeSlots` extension when available) |
| `apps/api/Jobuler.Api/Controllers/ShiftRequestsController.cs` | Replaced `UnprocessableEntity(new { error = ... })` in `Cancel` with `ProblemDetailsResults.Problem()` call (422, "shift-request-rejected") |
| `apps/api/Jobuler.Api/Controllers/ShiftRequestsController.cs` | Removed `ShiftRequestErrorResponse` record (no longer needed) |
| `apps/api/Jobuler.Api/Controllers/ShiftRequestsController.cs` | Added `using Jobuler.Api.Middleware;` for `ProblemDetailsResults` access |

## Key decisions

- **Type slug `shift-request-rejected`** used for both Submit and Cancel rejections — they share the same domain concept (a shift request operation was rejected).
- **`alternativeSlots` extension** is only included when `result.AlternativeSlots` is not null, keeping the response clean when no alternatives exist.
- **Removed `ShiftRequestErrorResponse`** entirely rather than deprecating — it had no other consumers in the codebase.

## How it connects

- Depends on `ProblemDetailsResults` helper (created in step 594) and `ProblemDetailsFactory` (step 593).
- Frontend clients consuming shift request errors will now receive RFC 7807 responses with `type`, `title`, `status`, `detail`, `instance`, `traceId`, and optionally `alternativeSlots`.
- The `alternativeSlots` extension property preserves the domain-specific data that was previously in the custom response shape.

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

The build should succeed with no errors or warnings related to the controller.

## What comes next

- Task 4.2: Migrate `WaitlistController` error responses similarly.
- Task 6.2: Unit tests for controller ProblemDetails responses (verifies `alternativeSlots` extension).

## Git commit

```bash
git add -A && git commit -m "feat(problem-details): migrate ShiftRequestsController to RFC 7807 ProblemDetails"
```
