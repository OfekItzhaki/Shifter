# Step 582 — Under-Scheduled Member Detection

## Phase

Self-Service Scheduling — Application Layer (Task 13.1)

## Purpose

Detects members who have fewer approved shifts than the configured `MinShiftsPerCycle` when a request window closes. This ensures group admins are alerted about members who haven't met their minimum shift obligations, and members themselves receive a warning to request additional shifts.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Scheduling/SelfService/Commands/CheckUnderScheduledMembersCommand.cs` | MediatR command, result DTOs, FluentValidation validator, and handler that queries under-scheduled members and sends notifications |

## Key decisions

1. **Early exit on MinShiftsPerCycle = 0**: If the group has no minimum requirement, the command short-circuits without querying shift requests.
2. **Notification to both admin and member**: Uses `INotificationService.NotifySpaceAdminsAsync` for group admin notification (Req 6.7) and creates individual `Notification` entities for each under-scheduled member (Req 5.4, 13.6).
3. **Push notification failure isolation**: Push delivery failures are caught and logged but never prevent in-app notification persistence (Req 13.7).
4. **No config = skip**: If no `SelfServiceConfig` exists for the group, the command logs a warning and returns successfully with an empty list.
5. **Metadata JSON**: Both admin and member notifications include structured metadata (groupId, cycleId, counts) for frontend consumption.

## How it connects

- Triggered by `CheckUnderScheduledMembersJob` (Task 16.5) when a request window closes
- Reads from `SelfServiceConfigs`, `GroupMemberships`, `ShiftRequests`, and `People` tables
- Uses `INotificationService` (existing) for admin notifications
- Uses `IPushNotificationSender` (existing) for push delivery to members
- Creates `Notification` entities directly for member-level in-app notifications

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build --no-restore
```

The command is invoked via MediatR: `mediator.Send(new CheckUnderScheduledMembersCommand(spaceId, groupId, cycleId))`.

## What comes next

- Task 13.2: Property test for under-scheduled detection (Property 14)
- Task 16.5: Background job that triggers this command on request window close
- Task 17.1: Full notification service integration for all self-service events

## Git commit

```bash
git add -A && git commit -m "feat(self-service): implement under-scheduled member detection command"
```
