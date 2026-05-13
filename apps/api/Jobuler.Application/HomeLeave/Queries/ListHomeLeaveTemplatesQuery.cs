using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Queries;

public record HomeLeaveTemplateDto(
    Guid Id,
    string Name,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours,
    DateTime CreatedAt);

public record ListHomeLeaveTemplatesQuery(Guid SpaceId) : IRequest<List<HomeLeaveTemplateDto>>;

public class ListHomeLeaveTemplatesQueryHandler : IRequestHandler<ListHomeLeaveTemplatesQuery, List<HomeLeaveTemplateDto>>
{
    private readonly AppDbContext _db;

    public ListHomeLeaveTemplatesQueryHandler(AppDbContext db) => _db = db;

    public async Task<List<HomeLeaveTemplateDto>> Handle(ListHomeLeaveTemplatesQuery req, CancellationToken ct)
    {
        return await _db.HomeLeaveTemplates.AsNoTracking()
            .Where(t => t.SpaceId == req.SpaceId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new HomeLeaveTemplateDto(
                t.Id,
                t.Name,
                t.MinRestHours,
                t.EligibilityThresholdHours,
                t.LeaveCapacity,
                t.LeaveDurationHours,
                t.CreatedAt))
            .ToListAsync(ct);
    }
}
