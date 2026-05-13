# 242 — Home-Leave API Client Types and Functions

## Phase

Feature: Home-Leave Slider — Frontend Integration

## Purpose

Create a dedicated TypeScript API client module for home-leave endpoints. Previously, the `HomeLeaveConfigPanel` component called `apiClient` directly without typed interfaces. This step introduces proper TypeScript types and reusable functions for the home-leave config GET/PUT endpoints and the new preview endpoint.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/api/homeLeave.ts` | New API client module with typed interfaces and functions for home-leave config and preview |

### Interfaces added

- `HomeLeaveConfigDto` — Response shape from `GET /spaces/{spaceId}/groups/{groupId}/home-leave-config`, includes `balanceValue`
- `UpdateHomeLeaveConfigPayload` — Request body for `PUT /spaces/{spaceId}/groups/{groupId}/home-leave-config`, with optional `balanceValue`
- `CoverageGapDto` — Nested type for coverage gap entries in preview response
- `HomeLeavePreviewResponse` — Response shape from `POST /spaces/{spaceId}/groups/{groupId}/home-leave-preview`

### Functions added

- `getHomeLeaveConfig(spaceId, groupId)` — Fetches current home-leave config
- `updateHomeLeaveConfig(spaceId, groupId, payload)` — Updates home-leave config (balance_value is optional for backward compat)
- `getHomeLeavePreview(spaceId, groupId, balanceValue)` — Triggers a preview solver run and returns impact summary

## Key decisions

- Created a separate `homeLeave.ts` module rather than adding to `groups.ts` or `constraints.ts` — home-leave is a distinct domain concept with its own controller
- `balanceValue` is optional in `UpdateHomeLeaveConfigPayload` to support backward compatibility (omitting it retains the stored value)
- Preview function uses POST (matching the backend endpoint) since it triggers a solver computation
- Types use camelCase to match the JSON serialization from ASP.NET Core (which uses camelCase by default)

## How it connects

- Used by `HomeLeaveConfigPanel` component (will be refactored to use these typed functions)
- Used by `useHomeLeavePreview` custom hook (task 8.2) for debounced preview requests
- Used by `BalanceSlider` integration (task 8.4) for saving balance value on form submit

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit
```

Verify no TypeScript errors in the new file.

## What comes next

- Task 8.1: `BalanceSlider` component uses these types
- Task 8.2: `useHomeLeavePreview` hook calls `getHomeLeavePreview`
- Task 8.3: `ImpactSummary` component consumes `HomeLeavePreviewResponse`
- Task 8.4: Integration wires slider to preview hook and save uses `updateHomeLeaveConfig`

## Git commit

```bash
git add -A && git commit -m "feat(home-leave-slider): add frontend API client types and functions for home-leave"
```
