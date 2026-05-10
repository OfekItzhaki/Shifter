using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Jobuler.Application.AI.Commands;

namespace Jobuler.Application.AI.Import;

/// <summary>
/// Parses CSV (and Excel) files with known column structure into ImportPreviewDto without AI.
/// Returns null if the file's columns don't match the expected schema.
/// </summary>
public class StructuredImportParser : IStructuredImportParser
{
    private const int MaxDataRows = 10_000;

    private static readonly char[] InjectionChars = { '=', '+', '-', '@', '\t', '\r' };

    /// <inheritdoc />
    public StructuredParseResult? TryParse(byte[] fileContent, string fileName)
    {
        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

        return extension switch
        {
            ".csv" => TryParseCsv(fileContent),
            ".xlsx" or ".xls" => TryParseExcel(fileContent),
            _ => null
        };
    }

    private StructuredParseResult? TryParseCsv(byte[] fileContent)
    {
        using var stream = new MemoryStream(fileContent);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            BadDataFound = null,
        };

        using var csv = new CsvReader(reader, config);

        // Read header row
        if (!csv.Read() || !csv.ReadHeader())
            return null;

        var headerRecord = csv.HeaderRecord;
        if (headerRecord == null || headerRecord.Length == 0)
            return null;

        // Sanitize headers and detect columns
        var sanitizedHeaders = headerRecord.Select(Sanitize).ToArray();
        var columnMapping = DetectColumns(sanitizedHeaders);
        if (columnMapping == null)
            return null;

        // Parse data rows
        var assignments = new List<ImportAssignmentDto>();
        var warnings = new List<string>();
        var peopleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var taskMap = new Dictionary<string, ImportTaskDto>(StringComparer.OrdinalIgnoreCase);
        var rowNumber = 1; // 1-based, header is row 1, data starts at row 2

        while (csv.Read())
        {
            rowNumber++;

            if (assignments.Count >= MaxDataRows)
            {
                warnings.Add($"Row {rowNumber}: Maximum row limit ({MaxDataRows}) reached, remaining rows skipped.");
                break;
            }

            var personName = GetSanitizedField(csv, columnMapping.PersonNameIndex);
            var taskName = GetSanitizedField(csv, columnMapping.TaskNameIndex);
            var dayRaw = GetSanitizedField(csv, columnMapping.DayOfWeekIndex);
            var startHourRaw = GetSanitizedField(csv, columnMapping.StartHourIndex);
            var endHourRaw = GetSanitizedField(csv, columnMapping.EndHourIndex);

            // Validate person_name
            if (string.IsNullOrWhiteSpace(personName))
            {
                warnings.Add($"Row {rowNumber}: Empty person name.");
                continue;
            }

            // Validate task_name
            if (string.IsNullOrWhiteSpace(taskName))
            {
                warnings.Add($"Row {rowNumber}: Empty task name.");
                continue;
            }

            // Validate day_of_week
            var normalizedDay = DayOfWeekMapper.Normalize(dayRaw ?? string.Empty);
            if (normalizedDay == null)
            {
                warnings.Add($"Row {rowNumber}: Unrecognized day of week '{dayRaw}'.");
                continue;
            }

            // Validate start_hour
            if (!int.TryParse(startHourRaw, out var startHour) || startHour < 0 || startHour > 23)
            {
                warnings.Add($"Row {rowNumber}: Invalid start hour '{startHourRaw}'.");
                continue;
            }

            // Validate end_hour
            if (!int.TryParse(endHourRaw, out var endHour) || endHour < 0 || endHour > 23)
            {
                warnings.Add($"Row {rowNumber}: Invalid end hour '{endHourRaw}'.");
                continue;
            }

            // Extract optional fields for task
            var shiftDuration = 4;
            var requiredHeadcount = 1;

            if (columnMapping.ShiftDurationIndex.HasValue)
            {
                var durationRaw = GetSanitizedField(csv, columnMapping.ShiftDurationIndex.Value);
                if (int.TryParse(durationRaw, out var dur) && dur > 0)
                    shiftDuration = dur;
            }

            if (columnMapping.RequiredHeadcountIndex.HasValue)
            {
                var headcountRaw = GetSanitizedField(csv, columnMapping.RequiredHeadcountIndex.Value);
                if (int.TryParse(headcountRaw, out var hc) && hc > 0)
                    requiredHeadcount = hc;
            }

            // Add assignment
            assignments.Add(new ImportAssignmentDto(
                personName.Trim(),
                taskName.Trim(),
                normalizedDay,
                startHour,
                endHour));

            // Track unique people
            peopleSet.Add(personName.Trim());

            // Track unique tasks (last seen values for optional fields win)
            var taskKey = taskName.Trim();
            taskMap[taskKey] = new ImportTaskDto(taskKey, shiftDuration, requiredHeadcount);
        }

        return new StructuredParseResult(
            peopleSet.ToList(),
            taskMap.Values.ToList(),
            assignments,
            warnings);
    }

    private StructuredParseResult? TryParseExcel(byte[] fileContent)
    {
        try
        {
            using var stream = new MemoryStream(fileContent);
            using var workbook = new XLWorkbook(stream);

            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                return null;

            var usedRange = worksheet.RangeUsed();
            if (usedRange == null)
                return null;

            var firstRow = usedRange.FirstRow();
            var columnCount = firstRow.CellCount();
            if (columnCount == 0)
                return null;

            // Read and sanitize headers
            var sanitizedHeaders = new string[columnCount];
            for (var i = 0; i < columnCount; i++)
            {
                var cellValue = firstRow.Cell(i + 1).GetString();
                sanitizedHeaders[i] = Sanitize(cellValue);
            }

            // Detect columns
            var columnMapping = DetectColumns(sanitizedHeaders);
            if (columnMapping == null)
                return null;

            // Parse data rows
            var assignments = new List<ImportAssignmentDto>();
            var warnings = new List<string>();
            var peopleSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var taskMap = new Dictionary<string, ImportTaskDto>(StringComparer.OrdinalIgnoreCase);

            var totalRows = usedRange.RowCount();
            for (var rowIdx = 2; rowIdx <= totalRows; rowIdx++)
            {
                var rowNumber = rowIdx; // 1-based row number in the worksheet

                if (assignments.Count >= MaxDataRows)
                {
                    warnings.Add($"Row {rowNumber}: Maximum row limit ({MaxDataRows}) reached, remaining rows skipped.");
                    break;
                }

                var row = worksheet.Row(rowIdx);

                var personName = GetSanitizedExcelCell(row, columnMapping.PersonNameIndex);
                var taskName = GetSanitizedExcelCell(row, columnMapping.TaskNameIndex);
                var dayRaw = GetSanitizedExcelCell(row, columnMapping.DayOfWeekIndex);
                var startHourRaw = GetSanitizedExcelCell(row, columnMapping.StartHourIndex);
                var endHourRaw = GetSanitizedExcelCell(row, columnMapping.EndHourIndex);

                // Validate person_name
                if (string.IsNullOrWhiteSpace(personName))
                {
                    warnings.Add($"Row {rowNumber}: Empty person name.");
                    continue;
                }

                // Validate task_name
                if (string.IsNullOrWhiteSpace(taskName))
                {
                    warnings.Add($"Row {rowNumber}: Empty task name.");
                    continue;
                }

                // Validate day_of_week
                var normalizedDay = DayOfWeekMapper.Normalize(dayRaw ?? string.Empty);
                if (normalizedDay == null)
                {
                    warnings.Add($"Row {rowNumber}: Unrecognized day of week '{dayRaw}'.");
                    continue;
                }

                // Validate start_hour
                if (!int.TryParse(startHourRaw, out var startHour) || startHour < 0 || startHour > 23)
                {
                    warnings.Add($"Row {rowNumber}: Invalid start hour '{startHourRaw}'.");
                    continue;
                }

                // Validate end_hour
                if (!int.TryParse(endHourRaw, out var endHour) || endHour < 0 || endHour > 23)
                {
                    warnings.Add($"Row {rowNumber}: Invalid end hour '{endHourRaw}'.");
                    continue;
                }

                // Extract optional fields for task
                var shiftDuration = 4;
                var requiredHeadcount = 1;

                if (columnMapping.ShiftDurationIndex.HasValue)
                {
                    var durationRaw = GetSanitizedExcelCell(row, columnMapping.ShiftDurationIndex.Value);
                    if (int.TryParse(durationRaw, out var dur) && dur > 0)
                        shiftDuration = dur;
                }

                if (columnMapping.RequiredHeadcountIndex.HasValue)
                {
                    var headcountRaw = GetSanitizedExcelCell(row, columnMapping.RequiredHeadcountIndex.Value);
                    if (int.TryParse(headcountRaw, out var hc) && hc > 0)
                        requiredHeadcount = hc;
                }

                // Add assignment
                assignments.Add(new ImportAssignmentDto(
                    personName.Trim(),
                    taskName.Trim(),
                    normalizedDay,
                    startHour,
                    endHour));

                // Track unique people
                peopleSet.Add(personName.Trim());

                // Track unique tasks (last seen values for optional fields win)
                var taskKey = taskName.Trim();
                taskMap[taskKey] = new ImportTaskDto(taskKey, shiftDuration, requiredHeadcount);
            }

            return new StructuredParseResult(
                peopleSet.ToList(),
                taskMap.Values.ToList(),
                assignments,
                warnings);
        }
        catch
        {
            // Corrupted or unsupported Excel file — return null to allow fallback
            return null;
        }
    }

    /// <summary>
    /// Gets a cell value from an Excel row by column index (0-based) and sanitizes it.
    /// </summary>
    private static string? GetSanitizedExcelCell(IXLRow row, int columnIndex)
    {
        try
        {
            var cell = row.Cell(columnIndex + 1); // ClosedXML uses 1-based indexing
            var value = cell.GetString();
            return string.IsNullOrEmpty(value) ? null : Sanitize(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Detects required and optional column indices from the header row.
    /// Returns null if any required column is missing.
    /// </summary>
    private static ColumnMapping? DetectColumns(string[] headers)
    {
        int? personNameIndex = null;
        int? taskNameIndex = null;
        int? dayOfWeekIndex = null;
        int? startHourIndex = null;
        int? endHourIndex = null;
        int? shiftDurationIndex = null;
        int? requiredHeadcountIndex = null;

        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim();

            if (personNameIndex == null && MatchesColumn(header, "person_name"))
                personNameIndex = i;
            else if (taskNameIndex == null && MatchesColumn(header, "task_name"))
                taskNameIndex = i;
            else if (dayOfWeekIndex == null && MatchesColumn(header, "day_of_week"))
                dayOfWeekIndex = i;
            else if (startHourIndex == null && MatchesColumn(header, "start_hour"))
                startHourIndex = i;
            else if (endHourIndex == null && MatchesColumn(header, "end_hour"))
                endHourIndex = i;
            else if (shiftDurationIndex == null && MatchesOptionalColumn(header, "shift_duration_hours"))
                shiftDurationIndex = i;
            else if (requiredHeadcountIndex == null && MatchesOptionalColumn(header, "required_headcount"))
                requiredHeadcountIndex = i;
        }

        // All required columns must be found
        if (personNameIndex == null || taskNameIndex == null || dayOfWeekIndex == null ||
            startHourIndex == null || endHourIndex == null)
            return null;

        return new ColumnMapping(
            personNameIndex.Value,
            taskNameIndex.Value,
            dayOfWeekIndex.Value,
            startHourIndex.Value,
            endHourIndex.Value,
            shiftDurationIndex,
            requiredHeadcountIndex);
    }

    private static bool MatchesColumn(string header, string canonicalKey)
    {
        if (!ImportColumnNames.RequiredColumns.TryGetValue(canonicalKey, out var variants))
            return false;

        return variants.Any(v => string.Equals(header, v, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesOptionalColumn(string header, string canonicalKey)
    {
        if (!ImportColumnNames.OptionalColumns.TryGetValue(canonicalKey, out var variants))
            return false;

        return variants.Any(v => string.Equals(header, v, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets a field value from the CSV reader by index and sanitizes it.
    /// </summary>
    private static string? GetSanitizedField(CsvReader csv, int index)
    {
        try
        {
            var value = csv.GetField(index);
            return value == null ? null : Sanitize(value);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Strips leading formula-injection characters from a cell value.
    /// Prevents CSV injection when data is later exported.
    /// </summary>
    internal static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var span = value.AsSpan();
        var startIndex = 0;

        while (startIndex < span.Length && InjectionChars.Contains(span[startIndex]))
        {
            startIndex++;
        }

        return startIndex == 0 ? value : span[startIndex..].ToString();
    }
}

/// <summary>
/// Maps detected column positions for structured import parsing.
/// </summary>
public record ColumnMapping(
    int PersonNameIndex,
    int TaskNameIndex,
    int DayOfWeekIndex,
    int StartHourIndex,
    int EndHourIndex,
    int? ShiftDurationIndex,
    int? RequiredHeadcountIndex);
