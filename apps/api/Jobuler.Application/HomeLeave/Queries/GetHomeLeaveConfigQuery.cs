using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Queries;

public record HomeLeaveConfigDto(
    Guid GroupId,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours);

public record GetHomeLeaveConfigQuery(Guid SpaceId, Guid GroupId) : IRequest<HomeLeaveConfigDto>;

public class GetHomeLeaveConfigQueryHandler : IRequestHandler<GetHomeLeaveConfigQuery, HomeLeaveConfigDto>
{
    private readonly AppDbContext _db;

    public GetHomeLeaveConfigQueryHandler(AppDbContext db) => _db = db;

    public async Task<HomeLeaveConfigDto> Handle(GetHomeLeaveConfigQuery req, CancellationToken ct)
    {
        var config = await _db.HomeLeaveConfigs.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == req.SpaceId && c.GroupId == req.GroupId, ct);

        if (config is null)
        {
            return new HomeLeaveConfigDto(
                GroupId: req.GroupId,
                MinRestHours: 8,
                EligibilityThresholdHours: 24,
                LeaveCapacity: 1,
                LeaveDurationHours: 48);
        }

        return new HomeLeaveConfigDto(
            GroupId: config.GroupId,
            MinRestHours: config.MinRestHours,
            EligibilityThresholdHours: config.EligibilityThresholdHours,
            LeaveCapacity: config.LeaveCapacity,
            LeaveDurationHours: config.LeaveDurationHours);
    }
}
