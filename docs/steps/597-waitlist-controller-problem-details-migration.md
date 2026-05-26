# Step 597 — WaitlistController Problem Details Migration

## Phase

Phase: Problem Details Migration (Spec: problem-details-migration, Task 4.2)

## Purpose

Migrate the `WaitlistController` error responses from the custom `WaitlistErrorResponse` shape to RFC 7807 ProblemDetails format, ensuring consistent error handling across the API.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Api/Controllers/WaitlistController.cs` | Replaced `UnprocessableEntity(new WaitlistErrorResponse(...))` with `ProblemDetailsResults.Problem()` in both `Join` and `AcceptOffer` endpoints. Removed the `WaitlistErrorResponse` record. Added `using Jobuler.Api.Middleware;` import. |

## Key decisions

- Used type slug `waitlist-rejected` to distinguish waitlist rejections from other 422 errors (e.g., `shift-request-rejected`, `unprocessable-entity`).
- Both `Join` and `AcceptOffer` use the same slug since they represent the same domain concept (waitlist operation rejected).
- Removed `WaitlistErrorResponse` entirely rather than deprecating it — no other code references it.

## How it connects

- Depends on `ProblemDetailsResults.Problem()` helper (created in task 1.3, step 594).
- Depends on `ProblemDetailsFactory.Create()` (created in task 1.1, step 593).
- Frontend clients consuming waitlist error responses will now receive `application/problem+json` with `type`, `title`, `status`, `detail`, `instance`, and `traceId` fields.
- Satisfies Requirements 9.3 (waitlist rejection returns ProblemDetails with 422) and 9.4 (uses extension properties instead of custom JSON shapes).

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build --no-restore
```

The build should succeed with no errors. Full integration testing is covered in task 6.2.

## What comes next

- Task 6.2 will add unit tests verifying the WaitlistController ProblemDetails response shape.
- Frontend waitlist error handling should be updated to parse the new `application/problem+json` format.

## Git commit

```bash
git add -A && git commit -m "feat(problem-details): migrate WaitlistController error responses to RFC 7807"
```
