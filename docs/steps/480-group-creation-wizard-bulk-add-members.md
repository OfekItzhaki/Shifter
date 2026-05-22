# 480 — Group Creation Wizard Modal & Member Bulk-Add

## Phase
UX Improvements

## Purpose
Two UX improvements to streamline group management:
1. Replace the inline name-input + button group creation with a proper modal wizard that combines naming and template selection in one step.
2. Add a "bulk add" mode to the member creation modal, allowing users to paste multiple names at once.

## What was built

### New files
- `apps/web/components/CreateGroupWizard.tsx` — Modal wizard component with name input + template selection cards. Auto-focuses the name input, shows template descriptions, and creates the group with the selected template on submit.
- `apps/web/components/BulkAddMembers.tsx` — Textarea-based bulk add component. Accepts one name per line, shows count preview, progress bar during addition, and success/error summary.

### Modified files
- `apps/web/app/groups/page.tsx` — Replaced inline form with a button that opens `CreateGroupWizard`. Added `handleCreateWithWizard` that creates the group and either applies the template picker (non-custom) or redirects directly (custom).
- `apps/web/app/groups/[groupId]/page.tsx` — Added `addMemberMode` state toggle (single/bulk), `handleBulkAdd` function that sequentially creates people and adds them to the group, and integrated `BulkAddMembers` component into the add-member modal.
- `apps/web/messages/he.json` — Added `groups.createWizard.*` and `groups.members_tab.bulk*` translation keys.
- `apps/web/messages/en.json` — Same keys in English.
- `apps/web/messages/ru.json` — Same keys in Russian.

## Key decisions
- The wizard combines name + template into a single step (no multi-step pagination) since there are only 5 templates — keeps it fast.
- Template picker in the wizard reuses `GROUP_TEMPLATES` from `@/lib/utils/groupTemplates` directly rather than the `GroupTemplatePicker` component, because the wizard only needs selection (not application). The existing `GroupTemplatePicker` is still used after creation to actually apply the template config.
- Bulk add processes names sequentially (not in parallel) to avoid rate limits and ensure consistent ordering.
- The bulk add reuses the same `searchPeople` → `createPerson` → `addGroupMemberById` pattern as the single-add handler.
- Progress is communicated via a callback from parent to child component, keeping state ownership clear.
- Dark mode support added to all new UI elements.

## How it connects
- `CreateGroupWizard` uses the existing `useCreateGroup` mutation and `GroupTemplatePicker` flow.
- `BulkAddMembers` uses the same `createPerson` and `addGroupMemberById` APIs as single-member creation.
- All text uses `useTranslations` with keys in all three locale files.

## How to run / verify
1. Navigate to `/groups` — click the "+ קבוצה חדשה" button → wizard modal should open with name input and template cards.
2. Enter a name, select a template, click "המשך" → group is created and template picker appears (or redirects for custom).
3. Navigate to a group → Members tab → click "+ הוסף חבר" → modal should show single/bulk toggle.
4. Switch to "הוספה מרובה" → paste multiple names → see count → click "הוסף הכל" → progress bar shows, then success message.

## What comes next
- Could add validation for duplicate names in bulk-add before submission.
- Could add drag-and-drop file upload for bulk member import (CSV/Excel already handled by ImportModal).

## Git commit
```bash
git add -A && git commit -m "feat(ux): group creation wizard modal and member bulk-add"
```
