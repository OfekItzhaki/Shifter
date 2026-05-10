# 132 — Manual Import Fallback (Complete Feature)

## Phase

Feature — Manual Import Fallback (structured CSV/Excel parsing without AI)

## Purpose

The existing schedule import required AI (GPT-4o) to parse uploaded files. This feature adds auto-detection logic so that structured CSV/Excel files with known columns are parsed deterministically without AI. If columns don't match, the system falls back to AI (if configured) or returns a clear error explaining the expected format.

This ensures the import feature works reliably without an AI API key configured.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/AI/Import/DayOfWeekMapper.cs` | Normalizes Hebrew/English day-of-week values to lowercase English |
| `apps/api/Jobuler.Application/AI/Import/ImportColumnNames.cs` | Defines required/optional column name mappings (Hebrew + English variants) |
| `apps/api/Jobuler.Application/AI/Import/IStructuredImportParser.cs` | Interface + result record for structured parsing |
| `apps/api/Jobuler.Application/AI/Import/StructuredImportParser.cs` | CSV + Excel parser with column detection, validation, injection prevention |
| `apps/api/Jobuler.Application/AI/Commands/SmartImportCommand.cs` | Modified handler: structured-first → AI fallback orchestration |
| `apps/api/Jobuler.Api/Controllers/ImportController.cs` | Added `GET /import/template` endpoint |
| `apps/api/Jobuler.Api/Program.cs` | DI registration for `IStructuredImportParser` |
| `apps/api/Jobuler.Application/Jobuler.Application.csproj` | Added CsvHelper 33.0.1 + ClosedXML 0.104.2 |
| `apps/web/components/SmartImportModal.tsx` | Field explanation, template download, parse method badge, warnings display |
| `apps/web/messages/he.json` | 7 new Hebrew translation keys for import UI |

## Key decisions

- **Auto-detection over tabs**: Instead of separate "AI" and "Manual" tabs, the system auto-detects file structure. Simpler UX.
- **CsvHelper + ClosedXML**: Standard .NET libraries for CSV/Excel. No COM dependencies.
- **CSV injection prevention**: Leading `=`, `+`, `-`, `@`, `\t`, `\r` stripped from all cell values.
- **Skip-and-warn**: Invalid rows are skipped with warnings rather than rejecting the entire file.
- **10,000 row limit**: Prevents memory exhaustion from very large files.
- **UTF-8 BOM in template**: Ensures Hebrew displays correctly when opened in Excel.
- **Same preview flow**: Whether parsed by CSV or AI, the same preview UI is shown.

## How it connects

- Extends the existing AI import flow (Task 10 from previous work)
- Uses existing `ImportPreviewDto`, `ImportTaskDto`, `ImportAssignmentDto` DTOs
- Same confirm endpoint creates people/tasks/draft schedule
- Frontend `SmartImportModal` in Settings tab unchanged in placement

## How to run / verify

1. Start the app (Docker or local)
2. Go to a group → Settings → "ייבוא חכם"
3. Click "הורד תבנית CSV" to download the template
4. Fill in the template with schedule data
5. Upload the CSV — should show "ניתוח מובנה" badge (no AI needed)
6. Upload an image/PDF — should use AI (if configured) or show error

## What comes next

- VPS deployment (Task 11)
- End-to-end testing with real schedule data
- Potential: add more column variants as users provide feedback

## Git commit

```bash
git add -A && git commit -m "feat(import): add structured CSV/Excel import fallback without AI"
```
