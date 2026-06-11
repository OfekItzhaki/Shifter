namespace Jobuler.Application.Scheduling.SelfService.Models;

public record AbsenceReportResult(
    bool Success,
    Guid? AbsenceReportId,
    bool WasLate,
    int AbsenceReportsUsed,
    int MaxAbsenceReports,
    int LateReportsUsed,
    int MaxLateReports,
    string? ErrorMessage);
