namespace Jobuler.Domain.Tasks;

/// <summary>
/// Describes how many people with a given qualification are needed per shift,
/// and whether that requirement is mandatory (hard) or preferred (soft).
/// </summary>
public record QualificationRequirement(
    string QualificationName,
    int Count,
    bool Mandatory);
