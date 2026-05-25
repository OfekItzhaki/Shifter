# 565 — Subscription/Billing UX Improvements

## Phase
Phase 8 — Billing & Subscription Polish

## Purpose
Improve the subscription/billing user experience by:
1. Guiding users to upgrade when solver failures are subscription-related
2. Hiding irrelevant recommendation banners when subscription is expired
3. Fixing the regeneration command to check space-level subscriptions (not deprecated group-level)
4. Showing subscription expiry warnings earlier (14 days instead of 7)

## What was built

### Modified files

| File | Change |
|------|--------|
| `apps/web/app/groups/[groupId]/tabs/ScheduleTab.tsx` | Added Link import, `subscriptionActive` prop, subscription keyword detection in solver error banner, shows "Upgrade" button (sky-500, links to /pricing) for subscription errors instead of "Run Again" |
| `apps/web/components/recommendations/RecommendationBanner.tsx` | Added `subscriptionActive` prop (default true), returns null when false |
| `apps/web/components/DraftScheduleModal.tsx` | Added `subscriptionActive` prop, passes it to RecommendationBanner |
| `apps/web/app/groups/[groupId]/page.tsx` | Added `getSpaceSubscription` import, `subscriptionActive` state, useEffect to fetch subscription status, passes prop to ScheduleTab and DraftScheduleModal |
| `apps/api/Jobuler.Application/Scheduling/Commands/TriggerRegenerationCommand.cs` | Changed from `GroupSubscriptions` to `SpaceSubscriptions`, uses `IsAccessGranted` instead of `IsActive` |
| `apps/web/components/billing/TrialBanner.tsx` | Changed threshold from 7 to 14 days, added sky color tier for 8-14 days |
| `apps/web/messages/he.json` | Added `"upgrade": "שדרג"` key to `schedule_tab` section |

## Key decisions

- **Subscription error detection**: Uses keyword matching (Hebrew, English, Russian) against the solver error message to determine if it's subscription-related. This is pragmatic since the error messages come from the backend in the user's locale.
- **Fail-open for subscription check**: If the subscription API call fails, we assume active (true) to avoid blocking users unnecessarily.
- **SpaceSubscription over GroupSubscription**: The regeneration command now uses the space-level subscription which is the canonical billing entity after the migration to space-level billing.
- **Three-tier color system for TrialBanner**: Red (≤3 days), amber (≤7 days), sky (≤14 days) provides graduated urgency.

## How it connects

- The solver error banner in ScheduleTab now provides a clear upgrade path when billing issues cause solver failures
- RecommendationBanner respects subscription status to avoid showing actionable suggestions users can't act on
- TriggerRegenerationCommand aligns with the space-billing migration (steps 482-501)
- TrialBanner gives users more advance notice before their subscription expires

## How to run / verify

1. TypeScript: `getDiagnostics` passes on all modified files
2. C# API: `dotnet build --no-restore` in `apps/api/` — succeeds with only pre-existing warnings
3. Manual: Set a space subscription to expired and verify the recommendation banner hides
4. Manual: Trigger a solver error with subscription-related message and verify the Upgrade button appears

## What comes next

- Consider adding a toast/notification when subscription expires mid-session
- The DraftScheduleModal could also show the Upgrade button for subscription-related solver errors

## Git commit

```bash
git add -A && git commit -m "feat(billing): subscription UX improvements — upgrade button, banner hiding, space subscription check, 14-day warning"
```
