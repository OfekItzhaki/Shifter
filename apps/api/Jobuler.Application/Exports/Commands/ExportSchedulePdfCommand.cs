using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Exports.Commands;

public record ExportSchedulePdfCommand(
    Guid SpaceId,
    Guid VersionId,
    Guid RequestingUserId) : IRequest<ExportPdfResult>;

public record ExportPdfResult(byte[] Content, string FileName);

/// <summary>
/// Collects the data needed for PDF generation.
/// Actual PDF rendering is done in Infrastructure (QuestPDF dependency lives there).
/// </summary>
public class ExportSchedulePdfCommandHandler
    : IRequestHandler<ExportSchedulePdfCommand, ExportPdfResult>
{
    private readonly AppDbContext _db;
    private readonly IPdfRenderer _renderer;

    public ExportSchedulePdfCommandHandler(AppDbContext db, IPdfRenderer renderer)
    {
        _db = db;
        _renderer = renderer;
    }

    public async Task<ExportPdfResult> Handle(ExportSchedulePdfCommand req, CancellationToken ct)
    {
        var version = await _db.ScheduleVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == req.VersionId && v.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Schedule version not found.");

        var space = await _db.Spaces.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == req.SpaceId, ct);

        var rows = await _db.Assignments.AsNoTracking()
            .Where(a => a.ScheduleVersionId == req.VersionId && a.SpaceId == req.SpaceId)
            .Join(_db.People.AsNoTracking(), a => a.PersonId, p => p.Id,
                (a, p) => new { a, PersonName = p.DisplayName ?? p.FullName })
            .Join(_db.TaskSlots.AsNoTracking(), x => x.a.TaskSlotId, s => s.Id,
                (x, s) => new { x.a, x.PersonName, Slot = s })
            .Join(_db.TaskTypes.AsNoTracking(), x => x.Slot.TaskTypeId, t => t.Id,
                (x, t) => new ScheduleRowDto(
                    x.PersonName,
                    t.Name,
                    t.BurdenLevel.ToString(),
                    x.Slot.StartsAt,
                    x.Slot.EndsAt,
                    x.Slot.Location))
            .OrderBy(r => r.StartsAt)
            .ToListAsync(ct);

        var model = new SchedulePdfModel(
            SpaceName: space?.Name ?? "Schedule",
            VersionNumber: version.VersionNumber,
            GeneratedAt: DateTime.UtcNow,
            Rows: rows);

        var bytes = _renderer.Render(model);
        var fileName = $"schedule-v{version.VersionNumber}-{DateTime.UtcNow:yyyyMMdd}.pdf";

        return new ExportPdfResult(bytes, fileName);
    }
}

public record ScheduleRowDto(
    string PersonName, string TaskName, string BurdenLevel,
    DateTime StartsAt, DateTime EndsAt, string? Location);

public record SchedulePdfModel(
    string SpaceName, int VersionNumber, DateTime GeneratedAt,
    List<ScheduleRowDto> Rows);
