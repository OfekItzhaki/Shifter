using Jobuler.Application.Scheduling.SelfService.Commands;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService.Queries;

// ── Get by ID ─────────────────────────────────────────────────────────────────

public record GetShiftTemplateQuery(Guid SpaceId, Guid GroupId, Guid TemplateId) : IRequest<ShiftTemplateDto?>;

public class GetShiftTemplateQueryHandler : IRequestHandler<GetShiftTemplateQuery, ShiftTemplateDto?>
{
    private readonly AppDbContext _db;

    public GetShiftTemplateQueryHandler(AppDbContext db) => _db = db;

    public async Task<ShiftTemplateDto?> Handle(GetShiftTemplateQuery req, CancellationToken ct)
    {
        var template = await _db.ShiftTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == req.TemplateId
                                   && t.GroupId == req.GroupId
                                   && t.SpaceId == req.SpaceId
                                   && !t.IsDeleted, ct);

        if (template is null)
            return null;

        return new ShiftTemplateDto(
            template.Id, template.GroupId, template.GroupTaskId, template.DayOfWeek,
            template.StartTime, template.EndTime, template.RequiredHeadcount,
            template.IsDeleted, template.CreatedAt, template.UpdatedAt);
    }
}

// ── List all for group ────────────────────────────────────────────────────────

public record ListShiftTemplatesQuery(Guid SpaceId, Guid GroupId, bool IncludeDeleted = false) : IRequest<IReadOnlyList<ShiftTemplateDto>>;

public class ListShiftTemplatesQueryHandler : IRequestHandler<ListShiftTemplatesQuery, IReadOnlyList<ShiftTemplateDto>>
{
    private readonly AppDbContext _db;

    public ListShiftTemplatesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ShiftTemplateDto>> Handle(ListShiftTemplatesQuery req, CancellationToken ct)
    {
        var query = _db.ShiftTemplates
            .AsNoTracking()
            .Where(t => t.GroupId == req.GroupId && t.SpaceId == req.SpaceId);

        if (!req.IncludeDeleted)
            query = query.Where(t => !t.IsDeleted);

        var templates = await query
            .OrderBy(t => t.DayOfWeek)
            .ThenBy(t => t.StartTime)
            .ToListAsync(ct);

        return templates.Select(t => new ShiftTemplateDto(
            t.Id, t.GroupId, t.GroupTaskId, t.DayOfWeek,
            t.StartTime, t.EndTime, t.RequiredHeadcount,
            t.IsDeleted, t.CreatedAt, t.UpdatedAt)).ToList();
    }
}
