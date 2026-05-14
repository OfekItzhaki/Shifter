using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Queries;

public record LoadHomeLeaveTemplateQuery(Guid SpaceId, Guid TemplateId) : IRequest<HomeLeaveTemplateDto>;

public class LoadHomeLeaveTemplateQueryHandler : IRequestHandler<LoadHomeLeaveTemplateQuery, HomeLeaveTemplateDto>
{
    private readonly AppDbContext _db;

    public LoadHomeLeaveTemplateQueryHandler(AppDbContext db) => _db = db;

    public async Task<HomeLeaveTemplateDto> Handle(LoadHomeLeaveTemplateQuery req, CancellationToken ct)
    {
        var template = await _db.HomeLeaveTemplates.AsNoTracking()
            .Where(t => t.Id == req.TemplateId && t.SpaceId == req.SpaceId)
            .Select(t => new HomeLeaveTemplateDto(
                t.Id,
                t.Name,
                t.MinRestHours,
                t.EligibilityThresholdHours,
                t.LeaveCapacity,
                t.LeaveDurationHours,
                t.CreatedAt))
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Template not found.");

        return template;
    }
}
