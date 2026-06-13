# Manual Self-Service QA Checklist

Use this checklist before demoing the manual self-service stack or before
approving `develop` for a `main` release PR.

## Preconditions

- API, web app, PostgreSQL, Redis, and worker services are running.
- Seed data includes the `Self-Service Demo` group and its demo special day.
- Demo users can log in with the configured E2E password.
- Browser local storage is clear or the tester knows which space/group is active.

## Member Smoke Test

Run these from `/pick` on desktop and mobile widths:

1. Select the `Self-Service Demo` group.
2. Pick an open shift.
3. Join a waitlist for a full shift.
4. Leave a waiting-list entry.
5. Cancel an owned future shift before the cutoff.
6. Report cannot attend for an owned shift.
7. Request a shift change.
8. Propose a shift swap.
9. Submit special leave.

Expected result: each action shows a clear success/error state, refreshes the
visible list, and preserves the member inside the same self-service group.

## Admin Smoke Test

Run these from the self-service group page while elevated for management:

1. Open Operations and confirm pending review counts are visible.
2. Approve and reject absence reports.
3. Approve and reject shift-change requests.
4. Approve and reject special leave.
5. Assign a member manually to a slot.
6. Remove a manually assigned member and confirm waitlist processing still works.
7. Mark attendance as present, no-show, or excused.
8. Open closeout and confirm coverage, attendance, waitlist, swaps, changes,
   absence, special leave, special-day impact, and override metrics are present.

Expected result: every admin decision updates the queue and preserves tenant,
space, and group scope.

## Automated Checks

Frontend:

```bash
cd apps/web
npm run build
npm run lint
npx playwright test e2e/self-service.browser.spec.ts --list
```

Backend:

```bash
cd apps/api
dotnet test Jobuler.sln --filter "FullyQualifiedName~SelfService|FullyQualifiedName~Waitlist|FullyQualifiedName~SpecialLeave|FullyQualifiedName~ShiftChange|FullyQualifiedName~ShiftSwap"
```

Full suite before merge:

```bash
cd apps/api
dotnet test Jobuler.sln
```

Client-ready package preflight:

```powershell
.\infra\scripts\test-customer-hosted-package.ps1
```

This checks the customer-hosted env validator, backup, restore dry-run, deploy
happy path, deploy rollback, Compose script syntax, and customer Compose config.

## Integration Checks

Holiday-calendar and portable/customer-hosted work have been integrated through
PR `#36` into `develop`. Keep these checks as regression coverage before
staging approval or a `develop` to `main` PR.

For holiday/special-day behavior:

- Verify special leave browser coverage still passes.
- Verify self-service slot browsing still labels slots that overlap space
  special days.
- Verify the self-service operations panel still shows special-day cycle counts
  and marks underfilled special-day slots.
- Verify closeout and exports include active member workflow policy flags,
  special-day slot, no-coverage special-day, and underfilled special-day counts.
- Run the smoke path from space special-day setup to a self-service cycle that
  visibly overlaps the marked day.
- Treat holiday-specific policy behavior, such as altered capacity or forced
  staffing rules on holidays, as future scope until a dedicated policy model is
  added.

For portable/customer-hosted behavior:

- Confirm special leave API/application/domain files are preserved.
- Confirm organization export, dry-run import validation, and package import
  include all self-service tables. Use compose backup/restore for full
  customer-hosted deployment restores.
- Confirm tenant/RLS checks cover member and admin self-service endpoints.
- Confirm billing behavior remains correct for current space-level plans.
