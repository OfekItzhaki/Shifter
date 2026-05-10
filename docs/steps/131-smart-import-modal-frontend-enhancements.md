# 131 — Smart Import Modal Frontend Enhancements

## Phase
Phase 4 — Manual Import Fallback (Frontend)

## Purpose
Enhance the SmartImportModal component to support the new structured parsing features: display expected column format, provide template download, show parse method badges, display row-skip warnings, and handle 422 errors with helpful guidance.

## What was built

### Modified files

- **`apps/web/components/SmartImportModal.tsx`**
  - Added `parseMethod` and `warnings` fields to the `ImportPreview` interface (Task 9.1)
  - Added field explanation section in idle state showing expected columns and optional columns (Task 9.2)
  - Added `handleDownloadTemplate` function using `apiClient` with blob response for authenticated download (Task 9.2)
  - Added parse method badge (emerald for structured, purple for AI) in the preview info box (Task 9.3)
  - Added warnings display (amber background, scrollable list) when rows are skipped (Task 9.3)
  - Updated error handler to detect 422 status and show template download link in error state (Task 9.4)

- **`apps/web/messages/he.json`**
  - Added 7 new translation keys to the `import` section (Task 10.1):
    - `fieldExplanation`, `downloadTemplate`, `parseMethodStructured`, `parseMethodAi`, `warningsTitle`, `expectedFormat`, `optionalColumns`

## Key decisions

- Used `apiClient.get` with `responseType: 'blob'` for template download instead of a plain `<a>` tag, since the app uses JWT-based auth (cookies won't carry the token)
- Template download silently fails — it's a convenience feature, not critical path
- Parse method badge uses color coding: emerald/green for structured (deterministic, reliable), purple for AI (probabilistic)
- Warnings use amber/yellow to indicate non-critical issues (rows skipped, not errors)
- Error state now always shows the template download link to guide users toward the correct format

## How it connects

- Consumes the `parseMethod` and `warnings` fields from the backend `ImportPreviewDto` (added in steps 128-129)
- Template download hits the endpoint created in step 130
- Translation keys are consumed by `next-intl`'s `useTranslations("import")` hook

## How to run / verify

```bash
cd apps/web
npx tsc --noEmit  # TypeScript check
```

Manually verify:
1. Open the import modal — field explanation and template download button should appear below the dropzone
2. Upload a structured CSV — preview should show a green "ניתוח מובנה" badge
3. Upload a file that triggers AI — preview should show a purple "ניתוח AI" badge
4. Upload a file with skipped rows — amber warnings section should appear
5. Upload a malformed file without AI configured — error should show with template download link

## What comes next

- End-to-end integration testing of the full structured import flow
- Potential addition of English locale translations

## Git commit

```bash
git add -A && git commit -m "feat(import): enhance SmartImportModal with field explanation, parse badges, and 422 handling"
```
