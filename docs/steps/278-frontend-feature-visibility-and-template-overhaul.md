# Step 278 — Frontend Feature Visibility Map & Template System Overhaul

## Phase

Phase 9 — Frontend template-aware UI

## Purpose

Transform the frontend from hardcoded domain assumptions into a template-driven system. The feature visibility map determines which UI features are shown based on the group's template type, the constraint system is generalized from `max_kitchen_per_week` to `max_task_type_per_period`, dead `min_rest_hours` seed data is removed from templates, and the group creation flow now persists the selected template type.

## What was built

| File | Description |
|------|-------------|
| `apps/web/lib/utils/templateFeatureConfig.ts` | New file: defines `GroupTemplateType`, `FeatureVisibility` interface, and `FEATURE_VISIBILITY_MAP` constant |
| `apps/web/lib/utils/groupTemplates.ts` | Removed all `min_rest_hours` constraint entries from template seed data |
| `apps/web/app/groups/[groupId]/tabs/ConstraintsTab.tsx` | Renamed `max_kitchen_per_week` to `max_task_type_per_period` in RULE_TYPES, updated `formatPayload`, updated default form state |
| `apps/web/components/ConstraintPayloadEditor.tsx` | Added `max_task_type_per_period` case with `task_type_name`, `max`, and `period_days` fields |
| `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` | Added feature visibility checks: conditionally renders closed-base toggle, home-leave panel, min-rest section; uses dynamic `stayoverLabel` |
| `apps/web/app/groups/[groupId]/page.tsx` | Passes `templateType` prop to SettingsTab |
| `apps/web/lib/api/groups.ts` | Added `templateType` to `GroupWithMemberCountDto` and `updateGroup` payload type |
| `apps/web/components/GroupTemplatePicker.tsx` | Sends `templateType` to API via `updateGroup` when applying a template |
| `apps/web/messages/he.json` | Updated constraint editor translations: added `periodDays`, updated `taskTypeName` and `maxKitchenPerWeek` labels |
| `apps/web/messages/en.json` | Updated constraint editor translations: added `periodDays`, updated `taskTypeName` and `maxKitchenPerWeek` labels |

## Key decisions

- **Feature visibility is frontend-only**: No API call needed — the map is a static TypeScript object keyed by template type. The group's `templateType` comes from the existing API response.
- **Graceful fallback**: If `templateType` is null/undefined or not in the map, defaults to `Custom` (all features visible).
- **Template type mapping**: Template IDs (`army-base`, `restaurant`, etc.) are mapped to API enum values (`Army`, `Restaurant`, etc.) in the picker.
- **Backward compatibility**: Existing groups without a `templateType` default to `Custom`, showing all features.
- **Label localization**: Uses `useLocale()` from next-intl to pick the correct `stayoverLabel` (he vs en).

## How it connects

- Depends on API task 7.1 which added `templateType` to the group response DTO
- The `FEATURE_VISIBILITY_MAP` is consumed by SettingsTab and can be imported by any other component needing template-aware rendering
- The `max_task_type_per_period` constraint type aligns with the solver's new generic constraint handler (task 5.2)
- Template seed data cleanup aligns with the group-level `MinRestBetweenShiftsHours` property which already handles rest enforcement

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # Should pass with zero errors
```

- Verify no `min_rest_hours` in `groupTemplates.ts`: `grep -r "min_rest_hours" apps/web/lib/utils/groupTemplates.ts` → no matches
- Verify no `max_kitchen_per_week` in ConstraintsTab: `grep -r "max_kitchen_per_week" apps/web/app/groups/` → no matches

## What comes next

- Task 9.6: Property test for feature visibility map completeness (fast-check)
- Task 11.2: Update next-intl messages for template-aware labels (full i18n integration)
- Checkpoint 10: Full frontend verification

## Git commit

```bash
git add -A && git commit -m "feat(phase9): frontend feature visibility map and template system overhaul"
```
