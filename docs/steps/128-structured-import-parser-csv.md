# 128 — Structured Import Parser (CSV)

## Phase

Manual Import Fallback — Structured Parsing Layer

## Purpose

Implements the core CSV parsing logic for the structured import fallback feature. This parser reads CSV files with known column headers (Hebrew and English variants), validates each row, sanitizes cell values against CSV injection, and extracts unique people and tasks — all without requiring AI.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/AI/Import/StructuredImportParser.cs` | Full implementation of `IStructuredImportParser` for CSV files. Includes column detection, CSV injection prevention, row validation with skip-and-warn, 10K row limit, and extraction of unique people/tasks. |

## Key decisions

- **CsvHelper** is used for robust CSV reading (handles BOM, quoted fields, encoding detection).
- Column detection iterates headers once and matches against `ImportColumnNames` variants case-insensitively.
- CSV injection prevention strips leading `=`, `+`, `-`, `@`, `\t`, `\r` from all cell values before any processing.
- Invalid rows are skipped (not rejected) with a warning message including the row number and reason.
- The `ColumnMapping` record is defined alongside the parser for locality.
- Task deduplication uses the last-seen optional field values (shift duration, headcount) for each unique task name.
- The parser returns `null` for non-CSV extensions (Excel support is a separate task).

## How it connects

- Implements `IStructuredImportParser` (defined in step 126).
- Uses `ImportColumnNames` (step 127) for column variant matching.
- Uses `DayOfWeekMapper` (step 126) for day normalization.
- Returns `StructuredParseResult` containing `ImportTaskDto` and `ImportAssignmentDto` from `Jobuler.Application.AI.Commands`.
- Will be injected into `ParseScheduleImportCommandHandler` in a later task (task 6).

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet build
```

Build should succeed with zero errors and zero warnings.

## What comes next

- Task 3.4: Property tests for the structured parser (round-trip, column detection, injection prevention, row validation).
- Task 4: Excel parsing support via ClosedXML.
- Task 6: Integration into the command handler with structured-first fallback logic.

## Git commit

```bash
git add -A && git commit -m "feat(import): implement StructuredImportParser with CSV parsing"
```
