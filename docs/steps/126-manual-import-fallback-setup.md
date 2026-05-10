# 126 — Manual Import Fallback: NuGet Packages & Project Setup

## Phase

Feature — Manual Import Fallback (structured CSV/Excel parsing)

## Purpose

Add the NuGet dependencies required for deterministic CSV and Excel parsing, and create the folder structure for the new import parser components. This is the foundation step that enables subsequent tasks to implement the structured parser.

## What was built

| File | Description |
|------|-------------|
| `apps/api/Jobuler.Application/Jobuler.Application.csproj` | Added `CsvHelper 33.0.1` and `ClosedXML 0.104.2` package references |
| `apps/api/Jobuler.Application/AI/Import/.gitkeep` | Created the `Import/` folder for parser components |

## Key decisions

- **CsvHelper 33.0.1**: Robust CSV parsing with BOM handling, quoted fields, and header mapping — standard choice for .NET CSV work.
- **ClosedXML 0.104.2**: Lightweight Excel reading without COM dependencies — reads .xlsx files using Open XML format.
- Folder placed under `AI/Import/` to co-locate with the existing AI import flow it extends.

## How it connects

- The `CsvHelper` package will be used by `StructuredImportParser` (Task 3) for CSV file reading.
- The `ClosedXML` package will be used by `StructuredImportParser` (Task 4) for Excel file reading.
- The `AI/Import/` folder will hold `DayOfWeekMapper`, `IStructuredImportParser`, `StructuredImportParser`, and `ImportColumnNames`.

## How to run / verify

```bash
cd apps/api/Jobuler.Application
dotnet restore
dotnet build --no-restore
```

Both commands should succeed with no errors.

## What comes next

Task 2: Implement `DayOfWeekMapper` utility for Hebrew/English day-of-week normalization.

## Git commit

```bash
git add -A && git commit -m "feat(import): add CsvHelper and ClosedXML packages for manual import fallback"
```
