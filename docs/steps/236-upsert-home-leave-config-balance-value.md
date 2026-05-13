# 236 — Update UpsertHomeLeaveConfigCommand to support balance_value

## Phase

Home-Leave Slider — Backend Application Layer

## Purpose

Extend the `UpsertHomeLeaveConfigCommand` and its handler to accept and persist the new `balance_value` parameter, enabling the slider position to be saved as part of the home-leave configuration. Maintains backward compatibility by treating a null `BalanceValue` as "retain the stored value."

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Application/HomeLeave/Commands/UpsertHomeLeaveConfigCommand.cs` | Added optional `int? BalanceValue = null` parameter to the command record; updated handler to pass `balanceValue` to `HomeLeaveConfig.Create` and `HomeLeaveConfig.Update`; added `BalanceValue` to `HomeLeaveConfigResult` DTO |

## Key decisions

- `BalanceValue` is nullable (`int?`) on the command to support backward compatibility — when omitted (null), the handler uses 50 for new configs and retains the stored value for existing configs (via the domain's `Update(int? balanceValue = null)` signature).
- The `HomeLeaveConfigResult` DTO now includes `BalanceValue` so callers always see the persisted value in the response.

## How it connects

- Depends on: Task 1.2 (domain entity already supports `BalanceValue` in `Create`/`Update`)
- Consumed by: Task 2.3 (controller will pass `BalanceValue` from the request DTO)
- Validated by: Task 2.5 (property test for balance value validation), Task 2.7 (backward compat property test)

## How to run / verify

```bash
cd apps/api
dotnet build
```

Build succeeds with zero errors.

## What comes next

- Task 2.2: Add FluentValidation rule for `BalanceValue`
- Task 2.3: Update controller PUT endpoint to pass `balance_value` from request body

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-slider): add BalanceValue to UpsertHomeLeaveConfigCommand and handler"
```
