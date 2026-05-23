# Step 506 — AuthController Lockout Response

## Phase

Phase: Admin Re-Auth Security

## Purpose

Update the `AuthController.ReAuthenticate` action to distinguish between authentication failure (401) and lockout (429) responses. When the handler indicates the user is locked out (5 failures in 15 minutes), the controller returns HTTP 429 with a JSON body containing the error message and retry-after duration, enabling the frontend to display appropriate lockout UI.

## What was built

| File | Description |
|------|-------------|
| `Jobuler.Api/Controllers/AuthController.cs` | Modified `ReAuthenticate` action to check `result.IsLockedOut` and return `StatusCode(429, { error, retryAfterSeconds })` when lockout is active, 401 for auth failure, and 200 for success |
| `Jobuler.Application/Auth/Commands/ReAuthenticateCommand.cs` | `ReAuthenticateResult` record includes `IsLockedOut` and `RetryAfterSeconds` properties to communicate lockout state from handler to controller |

## Key decisions

- **Three-way response mapping**: The controller maps the handler result to three distinct HTTP responses: 200 OK (success), 429 Too Many Requests (lockout), and 401 Unauthorized (auth failure). This gives the frontend clear signals for each state.
- **Fixed retry duration**: `retryAfterSeconds` is set to 900 (15 minutes) matching the lockout window, communicated from the handler via the result object rather than hardcoded in the controller.
- **JSON error body on 429**: Returns `{ error: "Too many attempts", retryAfterSeconds: 900 }` to match the API contract defined in the design document, enabling the frontend to show a countdown timer.

## How it connects

- **Upstream**: `ReAuthenticateCommandHandler` (task 1.3) performs the lockout check and returns `ReAuthenticateResult` with `IsLockedOut = true` and `RetryAfterSeconds = 900` when the user exceeds 5 failures in 15 minutes.
- **Downstream**: The frontend `ReAuthDialog` (task 3.3, 4.2) parses the 429 response to display "Too many attempts" and disable the submit button for the specified cooldown period.
- **Rate limiting**: The existing `[EnableRateLimiting("auth")]` attribute on the controller provides an additional layer of protection at the infrastructure level (Requirement 6.3).

## How to run / verify

```bash
# Build the API project
cd apps/api
dotnet build Jobuler.Api

# Verify the endpoint behavior manually:
# 1. Authenticate and get a valid JWT
# 2. Send 5 failed re-auth attempts within 15 minutes
# 3. The 6th attempt should return HTTP 429 with { error: "Too many attempts", retryAfterSeconds: 900 }
# 4. A successful re-auth should return HTTP 200 with { success: true }
# 5. A single failed attempt (not locked out) should return HTTP 401 with { error: "Authentication failed." }
```

## What comes next

- Task 1.5: Property test for audit log method and outcome correctness
- Task 1.6: Property test for audit log entry completeness
- Frontend tasks (3.3, 4.2): Handle the 429 response in `ReAuthDialog` to show lockout UI with countdown

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): controller returns 429 lockout response with retryAfterSeconds"
```
