# Step 511 — ReAuthDialog No DOM Rendering Property Test

## Phase

Phase 4 — Frontend autofill prevention and password form hardening (admin-reauth-security spec)

## Purpose

Validates Requirement 5.3: when the ReAuthDialog is not open (`open=false`), it must not render any DOM elements. This property-based test uses fast-check to verify this invariant holds across all possible prop combinations.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/admin/reAuthDialogNoDom.property.test.tsx` | Property-based test using fast-check (100 iterations) that generates arbitrary mode, spaceId, and callback props with `open=false` and asserts the container is empty |

## Key decisions

- Used `fc.constantFrom` for the `mode` prop to cover both "management" and "platform" variants
- Used `fc.oneof(fc.constant(undefined), fc.string())` for `spaceId` to cover both present and absent cases
- Asserts both `container.innerHTML === ""` and `container.childElementCount === 0` for comprehensive emptiness check
- Configured fast-check with `{ numRuns: 100 }` as specified in the design document

## How it connects

- Validates the `if (!open) return null` guard at line 444 of `ReAuthDialog.tsx`
- Complements the unit test in `reAuthDialogCredentials.test.tsx` that checks the same behavior with a single example
- Part of the admin-reauth-security spec's property testing suite (Properties 1–3)

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/admin/reAuthDialogNoDom.property.test.tsx
```

## What comes next

- Task 6.1: Verify login form retains password manager support
- Task 6.2: Write unit tests for scope isolation

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): property test for no DOM rendering when closed"
```
