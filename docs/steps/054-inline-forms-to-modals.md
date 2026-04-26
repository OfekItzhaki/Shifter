# Step 054 — Convert Inline Forms to Modals in Group Detail Page

## Phase
Phase 5 — UX Polish

## Purpose
All inline forms in the group detail page were rendered directly inside tab panels, causing layout shifts and cluttered UIs. This step moves every form into a `<Modal>` overlay, giving a consistent, focused editing experience across all tabs.

## What was built

### Modified
- `apps/web/app/groups/[groupId]/page.tsx`
  - Added `showAlertForm` state (default `false`) to gate the new-alert modal
  - Added `showAddMemberModal` state (default `false`) to gate the add-member modal
  - **Tasks tab**: "הוסף משימה" button now opens a `<Modal title="משימה חדשה / עריכת משימה">` instead of an inline form
  - **Constraints tab**: "+ אילוץ" button opens `<Modal title="אילוץ חדש">`; edit row replaced by `<Modal title="עריכת אילוץ" open={!!editingConstraintId}>`
  - **Alerts tab**: Always-visible create form replaced by a button + `<Modal title="התראה חדשה" open={showAlertForm}>`; inline edit replaced by `<Modal title="עריכת התראה" open={!!editingAlertId}>`
  - **Messages tab**: Inline edit replaced by `<Modal title="עריכת הודעה" open={!!editingMessageId}>`
  - **Members tab**: Inline add-by-email form replaced by `<Modal title="הוספת חבר" open={showAddMemberModal}>`; inline create-person form replaced by `<Modal title="הוספת אדם לפי שם" open={showCreatePersonForm}>`; inline invite form replaced by `<Modal title="שליחת הזמנה" open={!!invitingPersonId}>`
  - All modals rendered at the bottom of the main `return`, before the existing member-profile modal
  - Member-profile modal kept as-is (custom implementation)

## Key decisions
- Used IIFE (`{(() => { ... })()}`) inside Modal body where a local `inp` style variable was needed, avoiding prop drilling
- `showAddMemberModal` closes automatically on successful add (checks `!addError` after submit)
- `handleCreateAlert` now calls `setShowAlertForm(false)` on success
- Tab panels now only contain data lists + action buttons — zero inline forms

## How it connects
- Depends on `apps/web/components/Modal.tsx` (already imported)
- All existing state variables and handlers are unchanged — only render location moved

## How to run / verify
1. Start the dev server: `npm run dev` in `apps/web`
2. Open any group detail page and enter admin mode
3. Click "הוסף משימה", "+ אילוץ", "התראה חדשה", "הוסף לפי אימייל/טלפון", etc. — each should open a centered modal overlay
4. Press Escape or click the backdrop to close without saving
5. Run TypeScript check: `cmd /c "cd apps\web && node_modules\.bin\tsc --noEmit"`

## What comes next
- Further UX polish (e.g. form validation feedback, success toasts)
- Additional modal-based flows as new features are added

## Git commit
```bash
git add -A && git commit -m "feat(ux): convert all inline forms to modals in group detail page"
```
