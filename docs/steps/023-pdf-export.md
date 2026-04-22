# Step 023 — PDF Export

## Phase
Post-MVP Completion (spec section 21)

## Purpose
The spec requires "basic printable schedule view or PDF generation." CSV was already done. This step adds PDF export using QuestPDF — a landscape A4 table with person, task, burden level, times, and location, with page numbers.

## What was built

### Backend

| File | Description |
|---|---|
| `Application/Exports/IPdfRenderer.cs` | Interface defined in Application so the handler doesn't depend on QuestPDF directly |
| `Application/Exports/Commands/ExportSchedulePdfCommand.cs` | Command + handler that loads assignment data and delegates rendering to `IPdfRenderer`. Returns `ExportPdfResult(byte[], fileName)` |
| `Infrastructure/Exports/QuestPdfRenderer.cs` | QuestPDF implementation. Landscape A4, alternating row colors, header row, page numbers. Sets `LicenseType.Community` (free for open-source) |
| `Infrastructure/Jobuler.Infrastructure.csproj` | Added `QuestPDF` v2024.3.4 |
| `Api/Controllers/ExportsController.cs` | Added `GET /spaces/{id}/exports/{versionId}/pdf` endpoint |
| `Api/Program.cs` | Registered `IPdfRenderer → QuestPdfRenderer` as scoped |

### Frontend

| File | Description |
|---|---|
| `lib/api/schedule.ts` | Added `downloadExport(spaceId, versionId, format)` — fetches as blob and triggers browser download |
| `app/admin/schedule/page.tsx` | Added "↓ CSV" and "↓ PDF" buttons next to each version's action buttons |

## Key decisions

### QuestPDF over PuppeteerSharp
QuestPDF is a pure .NET library — no headless browser, no Chromium binary, no extra Docker layer. Community license is free. PuppeteerSharp would require a running browser and adds ~150MB to the container.

### IPdfRenderer interface in Application
Keeps QuestPDF out of the Application layer. The handler only knows about `IPdfRenderer` and `SchedulePdfModel` — both defined in Application. Infrastructure provides the implementation.

### Blob download on the frontend
The PDF endpoint returns `application/pdf` bytes. The frontend fetches with `responseType: "blob"` and creates a temporary object URL for the browser download dialog — no server-side file storage needed for on-demand exports.

## How to run / verify

1. Start the stack and trigger a solver run to create a schedule version
2. In Admin → Schedule, select any version
3. Click "↓ PDF" — browser downloads `schedule-vN-YYYYMMDD.pdf`
4. Open the PDF — should show a landscape table with all assignments
5. Via Swagger: `GET /spaces/{id}/exports/{versionId}/pdf` → returns PDF bytes

## What comes next
- End-to-end tests (Playwright)

## Git commit

```bash
git add -A && git commit -m "feat(exports): PDF export via QuestPDF"
```
