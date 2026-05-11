# 145 — Auto-Scheduler Improvements + Schedule History

## Phase
Phase 8 — Automation & UX

## Purpose
Two improvements: (1) Auto-publish option so groups can have schedules published automatically without admin review. (2) Schedule history browser so admins can view past versions and compare changes.

## What was built

### Auto-Publish:

| File | Change |
|------|--------|
| `apps/api/Jobuler.Domain/Groups/Group.cs` | Added `AutoPublish` property + `SetAutoPublish()` method |
| `apps/api/Jobuler.Infrastructure/Persistence/Configurations/GroupsConfiguration.cs` | Added `auto_publish` column mapping |
| `apps/api/Jobuler.Infrastructure/Scheduling/SolverWorkerService.cs` | After solver creates a feasible draft, checks if group has auto_publish=true and publishes immediately |
| `infra/migrations/039_group_auto_publish.sql` | Migration to add the column |

### Schedule History:

| File | Change |
|------|--------|
| `apps/web/components/schedule/ScheduleHistory.tsx` | Version list component — shows all published versions with "View Changes" button |
| `apps/web/messages/{en,he,ru}.json` | Added `schedule.history.*` translations |

## Auto-Publish Flow
1. Admin enables "Auto-publish" in group settings
2. Auto-scheduler triggers solver (every 6 hours)
3. Solver creates a feasible draft
4. System checks `group.AutoPublish == true`
5. Draft is published immediately (no admin review needed)
6. Notification sent to admins

## Schedule History UI
- Shows all published/archived versions sorted newest first
- Current version marked with green "Current" badge
- "View Changes" button opens the diff view for that version
- Reuses the existing ScheduleDiffView component

## Migration
```bash
cat /opt/shifter/infra/migrations/039_group_auto_publish.sql | docker compose exec -T postgres psql -U jobuler -d jobuler
```

## Git commit
```bash
git add -A && git commit -m "feat(phase8): auto-publish option, schedule history browser"
```
