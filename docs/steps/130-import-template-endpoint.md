# 130 — Import Template Download Endpoint

## Phase
Feature — Manual Import Fallback

## Purpose
Provide a downloadable CSV template so group admins know exactly which columns and format the structured import expects. The template includes Hebrew column headers and a sample data row, encoded with UTF-8 BOM for proper display in Excel.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Api/Controllers/ImportController.cs` | Added `GET /import/template` endpoint that returns a CSV file with BOM encoding |

## Key decisions
- **UTF-8 BOM prefix**: Excel on Windows requires the BOM bytes (`0xEF, 0xBB, 0xBF`) to correctly detect UTF-8 encoding for Hebrew characters.
- **Hebrew column headers**: Template uses the Hebrew column names (שם, משימה, יום, שעת_התחלה, שעת_סיום, משך_משמרת, נדרשים) since the target audience is Hebrew-speaking admins.
- **Sample row with geresh day**: The example row uses `א׳` (Sunday with geresh) to demonstrate the expected day format.
- **Permission check**: Requires `TasksManage` permission on the space, consistent with other import endpoints.

## How it connects
- The frontend `SmartImportModal` will link to this endpoint for template download (Task 9.2).
- The template columns match exactly what `StructuredImportParser` expects (Tasks 3.2, 3.3).
- The endpoint follows the same auth/permission pattern as the existing `Parse` and `Confirm` endpoints.

## How to run / verify
```bash
cd apps/api/Jobuler.Api
dotnet build
```

Test manually with an authenticated request:
```
GET /spaces/{spaceId}/groups/{groupId}/import/template
Authorization: Bearer <token>
```

Expected: 200 response with `Content-Type: text/csv; charset=utf-8` and file download `import-template.csv`.

## What comes next
- Task 7.2: Unit test for the template endpoint
- Task 9.2: Frontend template download link in SmartImportModal

## Git commit
```bash
git add -A && git commit -m "feat(import): add GET /import/template endpoint for CSV template download"
```
