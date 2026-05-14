# 246 — Token Refresh Mutex & Solver Performance Fix

## Phase
Production Bug Fixes

## Purpose
Fixes three production issues:
1. Users getting silently logged out due to concurrent token refresh requests revoking each other
2. Solver timing out due to O(n²) ISO string parsing in constraint-building phase
3. Solver error messages not providing actionable guidance when tasks can't be fully staffed

## What was built

### Files modified

- **`apps/web/lib/api/client.ts`** — Added a token refresh mutex. When multiple API calls fail with 401 simultaneously, only the first triggers a refresh; others queue and retry with the new token. Prevents the race condition where concurrent refresh requests revoke each other's tokens.

- **`apps/api/Jobuler.Api/Controllers/AuthController.cs`** — Added `[DisableRateLimiting]` to the `/auth/refresh` endpoint. The strict auth rate limiter (10/min in production) was blocking legitimate refresh requests.

- **`apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs`** — Improved the uncovered slots error message to be locale-aware and provide actionable guidance (reduce planning horizon, add members, or relax constraints).

- **`apps/solver/solver/constraints.py`** — **Critical performance fix**: Pre-compute all slot timestamps once before the O(n²) constraint loops. Previously, `_to_timestamp()` (ISO string parsing) was called inside nested loops, resulting in ~500K string parses for 133 slots × 14 people. Now timestamps are computed once (133 parses) and the loops use fast integer comparisons. Expected improvement: 15-20s → 1-3s.

## Key decisions

1. **Pre-compute timestamps**: The overlap and min-rest constraint functions now compute slot timestamps in a single pass, then iterate over pre-computed pairs. This changes the algorithm from O(people × slots² × parse_cost) to O(slots² + people × pairs).

2. **Separate pair detection from constraint addition**: Overlapping pairs and rest-violation pairs are identified once, then constraints are added per-person. This avoids redundant geometric checks.

3. **Refresh mutex pattern**: Uses a subscriber queue — the first 401 triggers the refresh, subsequent 401s subscribe to the result.

## How to run / verify

1. Deploy the solver container (rebuild required — Python code changed)
2. Deploy the API (C# changes)
3. Deploy the frontend (TypeScript changes)
4. Re-login once (current refresh token may be revoked)
5. Trigger the solver — should complete in 1-3 seconds instead of timing out

## What comes next

- Verify the schedule displays correctly after the solver produces assignments
- Monitor solver timing in production logs

## Git commit

```bash
git add -A && git commit -m "fix(solver): pre-compute timestamps to fix timeout + token refresh mutex"
```
