# 127 — Manual Import Fallback: ImportColumnNames Static Class

## Phase

Feature — Manual Import Fallback (structured CSV/Excel parsing)

## Purpose

Define the known column name mappings that the structured parser uses to detect whether a CSV/Excel file matches the expected schema. Each canonical column key maps to an array of accepted header variants in Hebrew and English, enabling bilingual column detection.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/AI/Import/ImportColumnNames.cs` | Static class with `RequiredColumns` and `OptionalColumns` dictionaries mapping canonical names to accepted header variants |

## Key decisions

- **Required columns**: `person_name`, `task_name`, `day_of_week`, `start_hour`, `end_hour` — all five must be present for structured parsing to succeed.
- **Optional columns**: `shift_duration_hours`, `required_headcount` — enhance output when present, use defaults (4 hours, 1 headcount) when absent.
- **Hebrew variants**: Each column accepts common Hebrew equivalents (e.g., שם, משימה, יום, שעת_התחלה, שעת_סיום) to support bilingual files.
- **Case-insensitive matching**: The dictionaries store lowercase variants; matching logic (in the parser) will compare case-insensitively.

## How it connects

- Used by `StructuredImportParser` (upcoming task) to detect and map file headers to canonical column positions.
- Used by the template download endpoint to generate the CSV template with correct headers.
- Used by the error response to list expected column names when structured parsing fails.

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build
```

Build should succeed with no errors.

## What comes next

Task 3.4: Create `IStructuredImportParser` interface and `StructuredParseResult` record.

## Git commit

```bash
git add -A && git commit -m "feat(import): add ImportColumnNames static class with Hebrew/English column mappings"
```
