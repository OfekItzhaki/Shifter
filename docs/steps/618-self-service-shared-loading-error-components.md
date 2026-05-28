# Step 618 — Self-Service Shared Loading & Error Components

## Phase

Self-Service Scheduling UI — Loading and Error State Components

## Purpose

Extract the repeated loading skeleton and error-with-retry patterns from all self-service tab components into shared, reusable components. This ensures visual consistency across tabs and reduces code duplication. Also provides a shared `MutationButton` component that handles spinner display and button disabling during in-flight requests.

## What was built

| File | Description |
|------|-------------|
| `apps/web/components/groups/selfService/LoadingCard.tsx` | Shared loading skeleton component with `list`, `form`, and `slots` variants |
| `apps/web/components/groups/selfService/ErrorRetry.tsx` | Shared error state component with Hebrew error message and "נסה שוב" retry button |
| `apps/web/components/groups/selfService/MutationButton.tsx` | Shared button component that shows spinner and disables during mutations |
| `apps/web/components/groups/selfService/index.ts` | Barrel export for all shared self-service components |
| `apps/web/components/groups/selfService/MyShiftsTab.tsx` | Updated to use `LoadingCard`, `ErrorRetry`, `MutationButton` |
| `apps/web/components/groups/selfService/ShiftTemplatesTab.tsx` | Updated to use `LoadingCard`, `ErrorRetry`, `MutationButton` |
| `apps/web/components/groups/selfService/SelfServiceConfigTab.tsx` | Updated to use `LoadingCard`, `ErrorRetry`, `MutationButton` |
| `apps/web/app/groups/[groupId]/tabs/SlotBrowserTab.tsx` | Updated to use `LoadingCard`, `ErrorRetry` |
| `apps/web/app/groups/[groupId]/tabs/WaitlistTab.tsx` | Updated to use `LoadingCard`, `ErrorRetry`, `MutationButton` |
| `apps/web/app/groups/[groupId]/tabs/SwapsTab.tsx` | Updated to use `LoadingCard`, `ErrorRetry`, `MutationButton` |
| `apps/web/app/groups/[groupId]/tabs/AdminOverridesTab.tsx` | Updated to use `LoadingCard`, `ErrorRetry`, `MutationButton` |

## Key decisions

1. **Three loading variants** — `list` (default, card rows with badge), `form` (label + input pairs), `slots` (card rows with action button) to match different tab layouts
2. **ErrorRetry uses i18n internally** — The component reads `selfService.error` and `selfService.retry` keys itself, so consumers only need to pass the error message and retry callback
3. **MutationButton handles all spinner logic** — Accepts `loading`, `label`, `loadingLabel`, and `variant` props. Automatically disables when loading. Supports `primary`, `danger`, and `secondary` variants.
4. **Barrel export** — `index.ts` allows clean imports: `import { LoadingCard, ErrorRetry, MutationButton } from "@/components/groups/selfService"`
5. **Accessibility** — `LoadingCard` includes `aria-busy="true"` and `aria-label` for screen readers. `ErrorRetry` uses `aria-hidden` on decorative SVG.

## How it connects

- All self-service tab components now use these shared components instead of inline loading/error markup
- The components use the existing `selfService.loading`, `selfService.error`, and `selfService.retry` i18n keys (already present in `he.json` and `en.json`)
- Mutations in all tabs already refetch data on success and restore state on failure (this was implemented in the individual tab tasks)
- The `MutationButton` pattern ensures requirement 12.3 (disable + spinner during in-flight) is consistently applied

## How to run / verify

1. Navigate to any self-service group tab — loading skeleton should appear briefly
2. Disconnect network or mock API failure — error card with "נסה שוב" button should appear
3. Click retry — data should refetch
4. Trigger any mutation (request shift, cancel, save config) — button should show spinner and disable
5. TypeScript check: `npx tsc --noEmit` (only pre-existing type errors remain)

## What comes next

- Task 17: Final checkpoint — Full UI integration verification

## Git commit

```bash
git add -A && git commit -m "feat(self-service-ui): shared LoadingCard, ErrorRetry, MutationButton components"
```
