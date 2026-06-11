namespace Jobuler.Application.Exports.Commands;

public record SelfServiceCloseoutPdfModel(
    string SpaceName,
    string GroupName,
    Guid CycleId,
    DateTime StartsAt,
    DateTime EndsAt,
    DateTime GeneratedAt,
    string ReportFingerprint,
    IReadOnlyList<SelfServiceCloseoutMetricDto> Metrics);

public record SelfServiceCloseoutMetricDto(string Label, string Value);
