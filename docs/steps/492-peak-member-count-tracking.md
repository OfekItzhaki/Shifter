# 492 — Peak Member Count Tracking on Member Addition

## Phase

Space-Level Billing — Infrastructure Wiring

## Purpose

Tracks the peak number of members in a space for billing purposes. When a new person is added to a space, the system compares the current member count against the stored peak and updates it if higher. This enables usage-based billing where the maximum number of members observed during a billing period determines the charge.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Billing/IPeakMemberTracker.cs` | Interface defining the `TrackAsync(spaceId)` contract |
| `apps/api/Jobuler.Infrastructure/Billing/PeakMemberTracker.cs` | Implementation that loads SpaceSubscription, counts active people in the space, and calls `UpdatePeakMemberCount` |
| `apps/api/Jobuler.Application/People/Commands/CreatePersonCommand.cs` | Updated handler to call `IPeakMemberTracker.TrackAsync` after creating a person |
| `apps/api/Jobuler.Application/Groups/Commands/AddPersonByEmailCommand.cs` | Updated handler to call `IPeakMemberTracker.TrackAsync` when a new person is created |
| `apps/api/Jobuler.Application/Groups/Commands/AddPersonByPhoneCommand.cs` | Updated handler to call `IPeakMemberTracker.TrackAsync` when a new person is created |
| `apps/api/Jobuler.Api/Program.cs` | Registered `IPeakMemberTracker` → `PeakMemberTracker` as scoped service |
| `apps/api/Jobuler.Tests/Helpers/NoOpPeakMemberTracker.cs` | No-op implementation for unit tests |
| `apps/api/Jobuler.Tests/InvitationFlow/PreservationTests.cs` | Updated to use `NoOpPeakMemberTracker` |

## Key decisions

- **Dedicated service over inline logic**: Created `IPeakMemberTracker` as a focused interface rather than duplicating the load-count-update logic in each handler. This keeps handlers clean and makes the billing concern testable in isolation.
- **Fail-silent design**: The `PeakMemberTracker` wraps all logic in try/catch and logs warnings on failure. Member addition should never fail because of billing tracking issues.
- **Track only on new person creation**: Peak tracking fires only when `isNew = true` (AddPersonByEmail/Phone) or always in CreatePersonCommand (which always creates). Adding an existing person to a group doesn't change the space-level member count.
- **Count active people**: Uses `People.Count(p => p.SpaceId == spaceId && p.IsActive)` as the member count metric, which represents the actual billable members in the space.

## How it connects

- **SpaceSubscription.UpdatePeakMemberCount(int)** — Domain method (task 1.1) that only updates if currentCount > PeakMemberCount
- **SpaceSubscription.ResetPeakForNewPeriod()** — Called when billing period changes (task 6.2 webhook handler)
- **Requirement 10.4** — "WHEN a member is added to the Space, THE Billing_Service SHALL compare the current member count to the stored peak member count"

## How to run / verify

```bash
cd apps/api
dotnet build --no-restore
dotnet test --filter "FullyQualifiedName~InvitationFlow.PreservationTests"
dotnet test --filter "FullyQualifiedName~UserRoleAssignmentFlowTests"
```

## What comes next

- Task 12: Backend complete checkpoint
- Frontend billing components (tasks 13–14)
- Property tests for peak member count tracking (task 1.5, Property 15)

## Git commit

```bash
git add -A && git commit -m "feat(billing): track peak member count on member addition to space"
```
