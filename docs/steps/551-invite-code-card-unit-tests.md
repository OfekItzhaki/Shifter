# Step 551 — Invite Code Card Unit Tests

## Phase
Phase 8 — Space Management Frontend Tests

## Purpose
Provides unit test coverage for the `InviteCodeCard` component, verifying that the invite code is displayed, the copy-to-clipboard functionality works, the regenerate flow calls the API and updates the UI, and the component is hidden for non-owners.

## What was built

| File | Description |
|------|-------------|
| `apps/web/__tests__/components/spaces/InviteCodeCard.test.tsx` | Unit tests for InviteCodeCard covering display, copy, regenerate, and permission gating |

## Key decisions
- Followed existing test patterns (vi.hoisted for mock functions, vi.mock for modules)
- Mocked `navigator.clipboard.writeText` via Object.assign for clipboard testing
- Used `vi.mock("next-intl")` returning key-based translations consistent with other tests
- Tested the full regenerate flow: click regenerate → confirmation UI → confirm → API call → updated display

## How it connects
- Tests validate Requirements 8.1, 8.2, 8.3, 8.4 from the space-management spec
- Component under test: `apps/web/components/spaces/InviteCodeCard.tsx`
- Mocked API: `@/lib/api/spaces` (`regenerateSpaceInviteCode`)

## How to run / verify
```bash
cd apps/web
npx vitest run __tests__/components/spaces/InviteCodeCard.test.tsx
```

## What comes next
- Additional frontend component tests (DangerZoneCard, RoleAssignmentCard, etc.)
- Final integration checkpoint for the space-management feature

## Git commit
```bash
git add -A && git commit -m "feat(space-management): add InviteCodeCard unit tests"
```
