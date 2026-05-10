# Implementation Plan: Manual Import Fallback

## Overview

Add structured CSV/Excel parsing to the existing AI-powered import flow. The implementation follows a bottom-up approach: build the parsing utilities first, then the parser service, then modify the orchestration handler, then add the template endpoint, and finally update the frontend. Each step builds on the previous one with no orphaned code.

## Tasks

- [x] 1. Add NuGet packages and project setup
  - Add CsvHelper and ClosedXML packages to `Jobuler.Application.csproj`
  - Create the `Import/` folder under `Jobuler.Application/AI/` for the new parser components
  - _Requirements: 2.3_

- [x] 2. Implement DayOfWeekMapper utility
  - [x] 2.1 Create `DayOfWeekMapper.cs` in `Jobuler.Application/AI/Import/`
    - Implement the static `Normalize(string input)` method
    - Include all Hebrew abbreviations (with/without geresh), full Hebrew names, English full/abbreviated names
    - Case-insensitive matching via `StringComparer.OrdinalIgnoreCase`
    - Return `null` for unrecognized values
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [ ]* 2.2 Write property tests for DayOfWeekMapper
    - **Property 4: Day Normalization Completeness**
    - **Property 5: Invalid Day Values Yield Null**
    - **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**

- [x] 3. Implement IStructuredImportParser with CSV parsing
  - [x] 3.1 Create `IStructuredImportParser.cs` interface in `Jobuler.Application/AI/Import/`
    - Define `StructuredParseResult? TryParse(byte[] fileContent, string fileName)`
    - Define `StructuredParseResult` record with People, Tasks, Assignments, and Warnings lists
    - _Requirements: 1.1, 1.6_

  - [x] 3.2 Create `StructuredImportParser.cs` implementing CSV parsing
    - Implement column detection: read first row, match against `ImportColumnNames` (Hebrew + English variants, case-insensitive)
    - Implement CSV injection prevention: strip leading `=`, `+`, `-`, `@`, `\t`, `\r` from all cell values
    - Implement row validation: skip rows with invalid hours (outside 0-23), empty names, or unrecognized days
    - Collect warnings for each skipped row (row number + reason)
    - Enforce 10,000 row maximum
    - Extract unique people and tasks from valid assignment rows
    - Use CsvHelper for robust CSV reading (BOM handling, quoted fields)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 9.1, 9.2, 10.1, 10.2, 10.3, 10.4, 10.5_

  - [x] 3.3 Create `ImportColumnNames.cs` static class
    - Define required column mappings (person_name, task_name, day_of_week, start_hour, end_hour) with Hebrew/English variants
    - Define optional column mappings (shift_duration_hours, required_headcount)
    - _Requirements: 1.2, 1.4_

  - [ ]* 3.4 Write property tests for StructuredImportParser (CSV)
    - **Property 1: Parse Round-Trip Consistency**
    - **Property 2: Column Detection Across Variants**
    - **Property 3: Missing Required Columns Yield Null**
    - **Property 6: CSV Injection Prevention**
    - **Property 7: Invalid Rows Are Skipped With Warnings**
    - **Validates: Requirements 1.1, 1.2, 1.3, 1.6, 9.1, 9.2, 10.1, 10.2, 10.3, 10.4**

- [x] 4. Implement Excel parsing in StructuredImportParser
  - [x] 4.1 Add Excel file detection and parsing to `StructuredImportParser`
    - Detect .xlsx/.xls extension and use ClosedXML to read the first worksheet
    - Convert worksheet rows to the same internal format as CSV rows
    - Apply identical column detection, sanitization, and validation logic
    - Wrap ClosedXML calls in try-catch: return null on any read failure (corrupted files)
    - _Requirements: 2.1, 2.2_

  - [ ]* 4.2 Write unit tests for Excel parsing
    - Test valid Excel file parsing produces same result as equivalent CSV
    - Test corrupted Excel file returns null without throwing
    - _Requirements: 2.1, 2.2_

- [x] 5. Checkpoint - Ensure parser tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Modify ParseScheduleImportCommandHandler for structured-first fallback
  - [x] 6.1 Update `ImportPreviewDto` to include `ParseMethod` field
    - Add `string? ParseMethod` parameter to the record (value: "structured" or "ai")
    - Update existing AI parsing path to set ParseMethod to "ai"
    - _Requirements: 8.1, 8.2, 8.3_

  - [x] 6.2 Inject `IStructuredImportParser` into `ParseScheduleImportCommandHandler`
    - Register `StructuredImportParser` as `IStructuredImportParser` in DI (Program.cs)
    - Add constructor parameter to the handler
    - _Requirements: 4.1_

  - [x] 6.3 Implement structured-first orchestration logic in the handler
    - For CSV/Excel extensions: decode base64 → call `TryParse` → if non-null, return with ParseMethod "structured"
    - If `TryParse` returns null: check if AI is configured → if yes, fall back to AI → if no, throw descriptive error
    - For image/PDF extensions: skip structured parsing, go directly to AI
    - If AI is not configured for image/PDF: throw descriptive error with HTTP 422
    - Handle empty result (zero valid rows): throw with HTTP 400 message
    - Include warnings from structured parsing in the response
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 7.1, 7.2_

  - [ ]* 6.4 Write unit tests for orchestration logic
    - Test structured parse success → AI not called, ParseMethod = "structured"
    - Test structured parse null + AI configured → AI called, ParseMethod = "ai"
    - Test structured parse null + AI not configured → 422 error with column list
    - Test image/PDF → AI called directly
    - Test image/PDF + no AI → 422 error
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

- [x] 7. Add template download endpoint
  - [x] 7.1 Add `GET /import/template` endpoint to `ImportController`
    - Require `[Authorize]` and space membership permission check
    - Generate CSV content with UTF-8 BOM encoding
    - Include all required + optional column headers
    - Include one sample data row with Hebrew day example
    - Return as `FileContentResult` with content-type `text/csv`
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [ ]* 7.2 Write unit test for template endpoint
    - Verify response is CSV with correct headers and BOM
    - Verify authentication is required
    - _Requirements: 5.1, 5.3, 5.4_

- [x] 8. Checkpoint - Ensure all backend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Update SmartImportModal frontend
  - [x] 9.1 Update `ImportPreview` TypeScript interface
    - Add `parseMethod: "structured" | "ai" | null` field
    - Add `warnings: string[]` optional field
    - _Requirements: 8.1, 10.4_

  - [x] 9.2 Add field explanation and template download to idle state
    - Add a collapsible/subtle section showing expected column names (Hebrew primary, English in parentheses)
    - Add a "הורד תבנית" (Download template) link pointing to `GET /import/template`
    - Style consistently with existing modal design (Tailwind, slate colors)
    - _Requirements: 6.1, 6.2_

  - [x] 9.3 Add parse method badge to preview state
    - Show a small badge/tag indicating "ניתוח מובנה" (structured) or "ניתוח AI" based on `parseMethod`
    - Display warnings list if present (yellow background, list of skipped row messages)
    - _Requirements: 6.3, 8.1_

  - [x] 9.4 Handle 422 error response in the modal
    - When API returns 422 with expected columns, display the column format info to the user
    - Show the template download link prominently in the error state
    - _Requirements: 7.1_

- [x] 10. Add Hebrew translations
  - [x] 10.1 Add new translation keys to the Hebrew locale file
    - `import.fieldExplanation` — explanation of expected columns
    - `import.downloadTemplate` — "הורד תבנית CSV"
    - `import.parseMethodStructured` — "ניתוח מובנה"
    - `import.parseMethodAi` — "ניתוח AI"
    - `import.warningsTitle` — "שורות שדולגו"
    - `import.expectedFormat` — error message with expected column format
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 11. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.
  - Verify structured CSV upload works end-to-end
  - Verify fallback to AI works when columns don't match
  - Verify template download works

- [x] 12. Step documentation
  - [x] 12.1 Create `docs/steps/` documentation for this feature
    - Document what was built, key decisions, how to verify
    - Include git commit command
    - _Requirements: N/A (process requirement)_

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- The parser must be fully built (tasks 2-4) before the handler modification (task 6) can use it
- Frontend changes (tasks 9-10) depend on the backend ParseMethod field being available (task 6.1)
