using System.Text.Json.Serialization;

namespace Jobuler.Domain.Tasks;

/// <summary>
/// Describes how many people with a given qualification are needed per shift,
/// and whether that requirement is mandatory (hard) or preferred (soft).
/// JsonPropertyName attributes ensure correct round-trip through JSONB storage
/// (stored as snake_case) and the Python solver payload.
/// </summary>
public record QualificationRequirement(
    [property: JsonPropertyName("qualification_name")] string QualificationName,
    [property: JsonPropertyName("count")]              int Count,
    [property: JsonPropertyName("mandatory")]          bool Mandatory);
