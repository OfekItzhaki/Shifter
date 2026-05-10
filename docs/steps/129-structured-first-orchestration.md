# 129 — Structured-First Orchestration in Import Handler

## Phase

Manual Import Fallback — Structured parsing integration

## Purpose

Wire the `StructuredImportParser` into the existing `ParseScheduleImportCommandHandler` so that CSV/Excel files are parsed deterministically first, falling back to AI only when structured parsing fails. This ensures the import feature works without an AI API key for files with known column structure.

## What was built

| File | Change |
|------|--------|
| `Jobuler.Application/AI/Commands/SmartImportCommand.cs` | Added `ParseMethod` and `Warnings` fields to `ImportPreviewDto`; injected `IStructuredImportParser` into handler; implemented structured-first orchestration logic with AI fallback |
| `Jobuler.Api/Program.cs` | Registered `StructuredImportParser` as `IStructuredImportParser` singleton in DI |

## Key decisions

- **Singleton lifetime** for `StructuredImportParser` — it's stateless (no DB, no HTTP) so a single instance is safe and efficient.
- **Structured-first for CSV/Excel** — the handler checks file extension and attempts structured parsing before AI. If `TryParse` returns null, it falls through to AI.
- **Direct-to-AI for images/PDFs** — no structured parsing is attempted for `.png`, `.jpg`, `.jpeg`, `.pdf`.
- **Descriptive errors** — when AI is not configured and structured parsing fails, the error message lists expected columns in both Hebrew and English. For images/PDFs without AI, a clear message directs the user to configure AI or use CSV/Excel.
- **Zero valid rows = HTTP 400** — if structured parsing succeeds but produces zero assignments, an `InvalidOperationException` is thrown (mapped to 400 by middleware).

## How it connects

- Depends on `IStructuredImportParser` and `StructuredImportParser` (step 128)
- Consumed by `ImportController.Parse` endpoint
- Frontend will use the new `ParseMethod` field to show a badge indicating parsing method
- `Warnings` field surfaces skipped-row information to the frontend preview

## How to run / verify

```bash
cd apps/api/Jobuler.Api
dotnet build
```

Build should succeed with zero errors.

## What comes next

- Frontend integration: display `ParseMethod` badge and warnings in the Smart Import Modal
- Template download endpoint
- Integration/E2E tests for the full structured → AI fallback flow

## Git commit

```bash
git add -A && git commit -m "feat(import): structured-first orchestration with AI fallback in handler"
```
