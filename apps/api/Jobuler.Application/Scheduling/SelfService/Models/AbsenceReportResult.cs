namespace Jobuler.Application.Scheduling.SelfService.Models;

public record AbsenceReportResult(
    bool Success,
    Guid? AbsenceReportId,
    bool WasLate,
    int LateReportsUsed,
    int MaxLateReports,
    string? ErrorMessage);
