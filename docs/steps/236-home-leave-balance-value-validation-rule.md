# Step 236: Home-Leave Balance Value FluentValidation Rule

## Phase
Feature — Home-Leave Slider

## Purpose
Add server-side input validation for the `BalanceValue` parameter on the `UpsertHomeLeaveConfigCommand`. When a client provides a `BalanceValue`, it must be between 0 and 100 inclusive. This prevents invalid values from reaching the domain layer or database.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/HomeLeave/Validators/UpsertHomeLeaveConfigValidator.cs` | Added `RuleFor(x => x.BalanceValue).InclusiveBetween(0, 100).When(x => x.BalanceValue.HasValue)` with Hebrew error message |

## Key decisions
- Validation only fires when `BalanceValue` is not null (`.When(x => x.BalanceValue.HasValue)`), preserving backward compatibility — omitting the field is valid.
- Error message is in Hebrew ("ערך האיזון חייב להיות בין 0 ל-100") consistent with the project's user-facing error messages.
- The rule lives in the existing `UpsertHomeLeaveConfigValidator` alongside other field validations.

## How it connects
- Depends on task 2.1 which added the `int? BalanceValue` parameter to `UpsertHomeLeaveConfigCommand`.
- The `ValidationBehavior` MediatR pipeline behavior automatically runs this validator before the handler executes.
- `ExceptionHandlingMiddleware` catches `ValidationException` and returns HTTP 400 with the error messages array.

## How to run / verify
```bash
cd apps/api
dotnet build   # should succeed with no new warnings
```

## What comes next
- Task 2.3: Update `HomeLeaveConfigController` PUT endpoint to accept `balance_value` in the request DTO.
- Task 2.5: Property-based test for balance value validation (FsCheck).

## Git commit
```bash
git add -A && git commit -m "feat(home-leave): add FluentValidation rule for BalanceValue"
```
