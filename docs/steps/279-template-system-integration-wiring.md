# 279 — Template System Integration Wiring

## Phase

Template System Overhaul — Integration & Cleanup (Tasks 11.1, 11.2)

## Purpose

Verify end-to-end wiring of `templateType` through the group creation/update flow and ensure template-aware labels support all three locales (en, he, ru).

## What was built

### Task 11.1 — Verification (no changes needed)

The following wiring was already complete from task 7.1:

- `CreateGroupCommand` includes `TemplateType` parameter → passed to `Group.Create(..., templateType)`
- `SetGroupTemplateTypeCommand` handler calls `group.SetTemplateType(req.TemplateType)`
- `GroupsController.CreateGroup` parses `templateType` from request and dispatches command
- `GroupsController.UpdateGroup` parses `templateType` and dispatches `SetGroupTemplateTypeCommand`
- `GetGroupsQuery` response (`GroupDto`) includes `TemplateType` field mapped from entity

### Task 11.2 — Russian locale support for stayover labels

| File | Change |
|------|--------|
| `apps/web/lib/utils/templateFeatureConfig.ts` | Added `ru` key to `stayoverLabel` interface and all template entries |
| `apps/web/app/groups/[groupId]/tabs/SettingsTab.tsx` | Changed locale lookup from `locale === "he" ? ... : ...en` to dynamic key lookup with English fallback |

Russian translations:
- Army/Security: "Закрытая база" (Closed Base)
- Restaurant/Hospital/Custom: "Ночёвка" (Stayover)

## Key decisions

- **No next-intl message keys needed**: The `stayoverLabel` is a static config object in `templateFeatureConfig.ts`, not a next-intl translation key. This is by design — the feature visibility map is frontend-only and doesn't require an API call.
- **Dynamic locale lookup**: Changed from hardcoded `locale === "he"` ternary to `visibility.stayoverLabel[locale]` with English fallback, supporting any future locale additions.

## How it connects

- Completes the template system overhaul integration layer (spec tasks 11.1 + 11.2)
- The `templateType` field flows: Frontend → API Controller → Command → Domain Entity → DB → Query Response → Frontend
- The `stayoverLabel` is consumed by `SettingsTab.tsx` to render the closed-base toggle section header

## How to run / verify

```bash
cd apps/api && dotnet build --no-restore -v q
cd apps/web && npx tsc --noEmit
```

Both pass with zero errors.

## What comes next

- Task 11.3: Property test for constraint migration semantics (FsCheck)
- Task 11.4: Property test for generic task-type counting (FsCheck)
- Task 11.5: Property test for template seeding correctness (FsCheck)
- Task 12: Final checkpoint — full integration verification

## Git commit

```bash
git add -A && git commit -m "feat(template-overhaul): wire template type e2e and add ru locale for stayover labels"
```
