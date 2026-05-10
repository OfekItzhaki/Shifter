using Jobuler.Application.AI.Commands;

namespace Jobuler.Application.AI.Import;

/// <summary>
/// Attempts to parse a CSV/Excel file using known column structure.
/// Returns null if columns don't match the expected schema, allowing fallback to AI parsing.
/// </summary>
public interface IStructuredImportParser
{
    /// <summary>
    /// Attempts to parse a CSV/Excel file using known column structure.
    /// Returns null if columns don't match the expected schema.
    /// </summary>
    StructuredParseResult? TryParse(byte[] fileContent, string fileName);
}

/// <summary>
/// Result of a successful structured parse operation.
/// Contains the extracted people, tasks, assignments, and any warnings about skipped rows.
/// </summary>
public record StructuredParseResult(
    List<string> People,
    List<ImportTaskDto> Tasks,
    List<ImportAssignmentDto> Assignments,
    List<string> Warnings);
