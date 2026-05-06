# Step 116 — Constants, Test Path Fix, Final Cleanup

## Phase
Phase 4 — Quality & Correctness

## Purpose
Final cleanup pass:
1. Extracted magic numbers to named constants
2. Fixed `group-detail-tabs.test.ts` path resolution (was using wrong `__dirname` depth)
3. Improved `ImageUpload` alt text from generic "Preview" to the component's label

## What was built

- **`apps/web/components/ImageUpload.tsx`** — Added `MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024` constant. Changed `alt="Preview"` to `alt={resolvedLabel}` for better accessibility.
- **`apps/web/lib/query/hooks/useGroups.ts`** — Added `GROUPS_STALE_MS = 30_000` and `DELETED_GROUPS_STALE_MS = 60_000` named constants with explanatory comments.
- **`apps/web/__tests__/group-detail-tabs.test.ts`** — Added `SOLVER_HORIZON_WARNING_THRESHOLD = 30` constant. Fixed `seed.sql` path resolution: was using 5 levels up (`../../../../../`) but needed 3 (`../../../`) when run with ts-node from `apps/web`.

## Test results
- Backend: **364/364 passing**
- Frontend: **9/9 test files passing**

## Git commit
```bash
git add -A && git commit -m "chore: extract magic numbers to constants, fix test path resolution, improve alt text"
```
