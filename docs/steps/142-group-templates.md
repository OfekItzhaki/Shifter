# 142 — Group Templates

## Phase
Phase 8 — Onboarding & UX

## Purpose
New users creating a group don't know what tasks and constraints to set up. Templates give them a one-click starting point with pre-configured tasks, shift durations, and constraints tailored to their industry.

## What was built

### Files created:

| File | Description |
|------|-------------|
| `apps/web/lib/utils/groupTemplates.ts` | Template definitions — 5 presets with tasks, constraints, and solver settings |
| `apps/web/components/GroupTemplatePicker.tsx` | Template selection UI with cards, task previews, and apply button |

### Files modified:

| File | Change |
|------|--------|
| `apps/web/app/groups/page.tsx` | Shows template picker modal after group creation |
| `apps/web/messages/en.json` | Added `groups.templates.*` translations |
| `apps/web/messages/he.json` | Same |
| `apps/web/messages/ru.json` | Same |

## Templates

| Template | Tasks | Constraints | Horizon |
|----------|-------|-------------|---------|
| **Army / Military Base** | Guard Duty (24h), Kitchen (8h), Patrol (8h) | 8h min rest, no consecutive hated | 7 days |
| **Restaurant / Cafe** | Morning (6h), Evening (6h), Closing (3h) | 10h min rest | 7 days |
| **Hospital / Clinic** | Day (8h), Evening (8h), Night (8h) | 12h min rest, no consecutive hated | 14 days |
| **Security / Guard** | Day Watch (12h), Night Watch (12h) | 10h min rest | 7 days |
| **Custom (Empty)** | None | None | 7 days |

## Flow

1. User creates a new group (enters name, clicks "New Group")
2. Template picker modal appears with 5 options
3. User selects a template and clicks "Apply Template"
4. System creates all tasks and constraints via existing APIs
5. User is redirected to the group detail page with everything set up
6. User can skip the template and set up manually

## Key decisions

1. **Frontend-only** — No new backend endpoints needed. Templates use the existing `createGroupTask` and `createConstraint` APIs.
2. **Non-destructive** — Templates only ADD tasks/constraints. They don't delete existing ones.
3. **Customizable after** — Users can edit/delete any template-created task or constraint.
4. **Industry-specific** — Each template is designed for a real use case with realistic shift durations and headcounts.

## How to run / verify
1. Go to Groups page
2. Create a new group
3. Template picker modal appears
4. Select "Army / Military Base"
5. Click "Apply Template"
6. Navigate to the group → Tasks tab → see Guard Duty, Kitchen, Patrol
7. Navigate to Constraints tab → see min rest and no consecutive burden

## Git commit

```bash
git add -A && git commit -m "feat(phase8): group templates — Army, Restaurant, Hospital, Security presets for quick onboarding"
```
