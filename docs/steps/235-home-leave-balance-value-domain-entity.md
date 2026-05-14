# 235 — Home-Leave Balance Value Domain Entity Update

## Phase

Feature: Home-Leave Slider (Spec: `home-leave-slider`, Task 1.2)

## Purpose

Extend the `HomeLeaveConfig` domain entity with a `BalanceValue` property (0–100, default 50) so the solver can use the admin's slider position to control home-leave scheduling priority. This is the domain-layer foundation for the balance slider feature.

## What was built

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Groups/HomeLeaveConfig.cs` | Added `BalanceValue` property, updated `Create` and `Update` methods, added `ValidateBalanceValue` |

### Details

- **Property**: `public int BalanceValue { get; private set; } = 50;`
- **Create**: New optional parameter `int balanceValue = 50` — validates and sets on creation
- **Update**: New optional parameter `int? balanceValue = null` — if provided, validates and sets; if null, retains existing value (backward compatible)
- **ValidateBalanceValue**: Throws `InvalidOperationException` with Hebrew message "ערך האיזון חייב להיות בין 0 ל-100" if value is outside [0, 100]

## Key decisions

1. **Optional parameters** — Both `Create` and `Update` use optional parameters so existing callers (e.g., `UpsertHomeLeaveConfigHandler`) continue to compile without changes.
2. **Null semantics on Update** — `int? balanceValue = null` means "don't change the stored value", supporting backward compatibility (Requirement 10.3).
3. **Hebrew error message** — Consistent with the app's Hebrew-first approach for user-facing validation messages.
4. **Static validation method** — Follows the existing pattern used by `ValidateMinRestHours`, `ValidateLeaveCapacity`, etc.

## How it connects

- **Depends on**: Task 1.1 (DB migration adding `balance_value` column with CHECK constraint)
- **Used by**: Task 1.3 (EF Core mapping), Task 2.1 (application handler passes value through)
- **Validates**: Requirements 9.4, 9.5, 1.3

## How to run / verify

```bash
cd apps/api
dotnet build Jobuler.Domain/Jobuler.Domain.csproj
dotnet test Jobuler.Tests/Jobuler.Tests.csproj --filter "FullyQualifiedName~HomeLeaveConfig"
```

All 38 existing HomeLeaveConfig tests pass without modification.

## What comes next

- Task 1.3: EF Core configuration mapping for the new `BalanceValue` column
- Task 2.1: Update `UpsertHomeLeaveConfigCommand` to pass `balanceValue` through

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-slider): add BalanceValue property to HomeLeaveConfig domain entity"
```
