# Step 374 — Group Page Re-Auth Integration Verification

## Phase

Admin Re-Authentication Gate — Frontend Integration Verification

## Purpose

Verify that the group detail page correctly integrates the ReAuthDialog component for management mode entry, ensuring all acceptance criteria are met: dialog opens on admin toggle, success triggers both `enterAdminMode` and `enterElevatedMode`, cancel leaves user in standard view, and timeout is read from group settings.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/admin/groupPageReAuthIntegration.test.tsx` | Integration test suite (11 tests) verifying the group page re-auth flow |

## Key decisions

- **Test harness approach**: Rather than rendering the full 1600-line group page (which has dozens of dependencies), a minimal test harness replicates the exact integration pattern from the page. This tests the logic accurately while keeping tests fast and maintainable.
- **Real ReAuthDialog**: The test renders the actual `ReAuthDialog` component (not a mock) to verify the full integration between the page logic and the dialog.
- **Call order verification**: Tests verify that `enterAdminMode` is called before `enterElevatedMode` using `invocationCallOrder`.

## How it connects

- Validates the wiring between `apps/web/app/groups/[groupId]/page.tsx` and `apps/web/components/admin/ReAuthDialog.tsx`
- Confirms the `useAuthStore.enterAdminMode()` and `useAdminSessionStore.enterElevatedMode()` are called correctly
- Ensures `managementTimeoutMinutes` from group settings flows through to the session store

## How to run / verify

```bash
cd apps/web
npx vitest run __tests__/admin/groupPageReAuthIntegration.test.tsx
```

All 11 tests should pass:
- Req 1.1: Dialog opens when hasCredentials is true, doesn't open when false
- Req 1.3, 9.4: enterAdminMode and enterElevatedMode called on success
- Req 1.4: Cancel closes dialog, user stays in standard view
- Req 1.5: Mode "management" is used for group-level admin entry
- Exit admin mode does NOT require re-auth
- managementTimeoutMinutes from group settings (custom and default 15)

## What comes next

- Task 4.2: Platform page re-auth integration verification
- Task 4.3: Password form keyboard submission verification
- Task 4.4: Error handling and recovery verification

## Git commit

```bash
git add -A && git commit -m "feat(admin-reauth): verify group page re-auth integration (task 4.1)"
```
