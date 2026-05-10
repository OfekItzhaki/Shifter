# Requirements Document

## Introduction

The Manual Import Fallback feature extends the existing AI-powered schedule import flow with deterministic structured CSV/Excel parsing. When a file with known column structure is uploaded, the system parses it directly without AI involvement. If columns don't match the expected schema, the system falls back to AI parsing (when configured) or returns a descriptive error. This ensures the import feature works reliably without an AI API key while preserving the AI flow for unstructured files.

## Glossary

- **Structured_Parser**: The `IStructuredImportParser` component that attempts to parse CSV/Excel files using known column schemas
- **Import_Controller**: The API controller handling file upload, parsing orchestration, and template download
- **Smart_Import_Modal**: The frontend React component providing the file upload UI, preview display, and template download
- **Day_Mapper**: The `DayOfWeekMapper` utility that normalizes Hebrew/English day-of-week values to lowercase English
- **Import_Preview**: The `ImportPreviewDto` response containing parsed people, tasks, assignments, and metadata
- **Parse_Method**: A string field ("structured" or "ai") indicating which parsing strategy produced the result
- **Column_Schema**: The set of required and optional column names (in Hebrew and English variants) that define a valid structured file
- **CSV_Injection**: A security attack where spreadsheet formula characters at the start of cell values execute unintended operations

## Requirements

### Requirement 1: Structured CSV Parsing

**User Story:** As a group admin, I want to upload a CSV file with known columns so that my schedule data is parsed deterministically without requiring AI configuration.

#### Acceptance Criteria

1. WHEN a CSV file is uploaded with all required columns (person_name, task_name, day_of_week, start_hour, end_hour), THE Structured_Parser SHALL parse the file and return a valid Import_Preview
2. WHEN matching column headers, THE Structured_Parser SHALL accept Hebrew variants (שם, משימה, יום, שעת_התחלה, שעת_סיום) as equivalent to English column names
3. WHEN matching column headers, THE Structured_Parser SHALL perform case-insensitive comparison
4. WHEN optional columns (shift_duration_hours, required_headcount) are present, THE Structured_Parser SHALL include their values in the parsed output
5. WHEN optional columns are absent, THE Structured_Parser SHALL use default values (shift_duration_hours=4, required_headcount=1)
6. WHEN a CSV file is missing one or more required columns, THE Structured_Parser SHALL return null to indicate structured parsing is not possible

### Requirement 2: Excel Parsing Support

**User Story:** As a group admin, I want to upload Excel (.xlsx/.xls) files so that I can import schedules from spreadsheets without converting to CSV first.

#### Acceptance Criteria

1. WHEN an Excel file (.xlsx or .xls) is uploaded, THE Structured_Parser SHALL read the first worksheet and apply the same column detection logic as CSV
2. WHEN an Excel file is corrupted or uses unsupported features, THE Structured_Parser SHALL return null to allow fallback to AI parsing
3. THE Structured_Parser SHALL use the ClosedXML library for Excel file reading

### Requirement 3: Day-of-Week Normalization

**User Story:** As a group admin, I want to enter day names in Hebrew (abbreviations or full names) or English so that the system accepts my natural input format.

#### Acceptance Criteria

1. WHEN a day_of_week cell contains a Hebrew abbreviation with geresh (א׳, ב׳, ג׳, ד׳, ה׳, ו׳, ש׳), THE Day_Mapper SHALL normalize it to the corresponding lowercase English day name
2. WHEN a day_of_week cell contains a Hebrew abbreviation without geresh (א, ב, ג, ד, ה, ו, ש), THE Day_Mapper SHALL normalize it to the corresponding lowercase English day name
3. WHEN a day_of_week cell contains a full Hebrew day name (ראשון, שני, שלישי, רביעי, חמישי, שישי, שבת), THE Day_Mapper SHALL normalize it to the corresponding lowercase English day name
4. WHEN a day_of_week cell contains an English day name (full or abbreviated, any case), THE Day_Mapper SHALL normalize it to lowercase English
5. WHEN a day_of_week cell contains an unrecognized value, THE Day_Mapper SHALL return null indicating the value is invalid

### Requirement 4: Auto-Detection and Fallback Logic

**User Story:** As a group admin, I want the system to automatically detect whether my file can be parsed structurally so that I get fast deterministic results when possible and AI-powered parsing when needed.

#### Acceptance Criteria

1. WHEN a CSV or Excel file is uploaded, THE Import_Controller SHALL attempt structured parsing first before AI parsing
2. WHEN structured parsing succeeds, THE Import_Controller SHALL return the result with ParseMethod set to "structured" without invoking AI
3. WHEN structured parsing returns null and AI is configured, THE Import_Controller SHALL fall back to AI parsing and return the result with ParseMethod set to "ai"
4. WHEN structured parsing returns null and AI is not configured, THE Import_Controller SHALL return HTTP 422 with an error message listing the expected column format
5. WHEN an image or PDF file is uploaded, THE Import_Controller SHALL skip structured parsing and send directly to AI parsing
6. WHEN an image or PDF file is uploaded and AI is not configured, THE Import_Controller SHALL return HTTP 422 with an error indicating AI configuration is required

### Requirement 5: Template Download

**User Story:** As a group admin, I want to download a CSV template so that I know exactly which columns and format the system expects.

#### Acceptance Criteria

1. WHEN a GET request is made to the template endpoint, THE Import_Controller SHALL return a CSV file with all required and optional column headers
2. THE template CSV SHALL include a sample data row demonstrating the expected format
3. THE template endpoint SHALL require authentication and space membership permission
4. THE template file SHALL use UTF-8 encoding with BOM for proper Hebrew display in Excel

### Requirement 6: Frontend Field Explanation and Template Download

**User Story:** As a group admin, I want to see which columns are expected and download a template directly from the upload modal so that I can prepare my file correctly before uploading.

#### Acceptance Criteria

1. WHILE the Smart_Import_Modal is in idle state, THE Smart_Import_Modal SHALL display a field explanation listing the expected column names
2. WHILE the Smart_Import_Modal is in idle state, THE Smart_Import_Modal SHALL display a clickable link to download the CSV template
3. WHEN the preview is displayed, THE Smart_Import_Modal SHALL show a badge indicating whether the result was parsed via "structured" or "ai" method

### Requirement 7: Error Handling

**User Story:** As a group admin, I want clear error messages when my file cannot be parsed so that I know how to fix the issue.

#### Acceptance Criteria

1. IF structured parsing fails and AI is not configured, THEN THE Import_Controller SHALL return a 422 response containing the list of expected column names in both Hebrew and English
2. IF a file contains headers but zero valid data rows, THEN THE Import_Controller SHALL return HTTP 400 with the message "File contains no valid data rows"
3. IF an Excel file cannot be read due to corruption, THEN THE Structured_Parser SHALL return null without throwing an exception

### Requirement 8: Parse Method Indicator

**User Story:** As a group admin, I want to know whether my file was parsed structurally or by AI so that I can assess the reliability of the preview.

#### Acceptance Criteria

1. THE Import_Preview SHALL include a ParseMethod field with value "structured" or "ai"
2. WHEN structured parsing produces the result, THE Import_Controller SHALL set ParseMethod to "structured"
3. WHEN AI parsing produces the result, THE Import_Controller SHALL set ParseMethod to "ai"

### Requirement 9: CSV Injection Prevention

**User Story:** As a system operator, I want cell values to be sanitized so that formula injection attacks are prevented when data is later exported.

#### Acceptance Criteria

1. WHEN parsing cell values, THE Structured_Parser SHALL strip leading characters that could trigger formula execution (=, +, -, @, \t, \r)
2. THE Structured_Parser SHALL apply sanitization before any other validation or processing of cell values

### Requirement 10: Row Validation with Skip-and-Warn

**User Story:** As a group admin, I want invalid rows to be skipped with warnings rather than rejecting the entire file so that I can still import the valid portion of my data.

#### Acceptance Criteria

1. WHEN a data row has an invalid start_hour or end_hour (outside 0-23 range), THE Structured_Parser SHALL skip that row and record a warning
2. WHEN a data row has an empty person_name or task_name, THE Structured_Parser SHALL skip that row and record a warning
3. WHEN a data row has an unrecognized day_of_week value, THE Structured_Parser SHALL skip that row and record a warning
4. WHEN rows are skipped, THE Import_Preview SHALL include a warnings list indicating which rows were skipped and why
5. THE Structured_Parser SHALL enforce a maximum of 10,000 data rows to prevent memory exhaustion
