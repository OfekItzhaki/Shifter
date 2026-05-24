using Jobuler.Domain.Groups;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Queries;

public record GetSpaceHomeLeaveConfigQuery(Guid SpaceId) : IRequest<SpaceHomeLeaveConfigDto?>;

public record SpaceHomeLeaveConfigDto(
    HomeLeaveMode Mode,
    int BalanceValue,
    int BaseDays,
    int HomeDays,
    int MinPeopleAtBase,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours,
    bool EmergencyFreezeActive,
    bool EmergencyUseForScheduling,
    DateTime? FreezeStartedAt,
    HomeLeaveMode PreFreezeMode);

public class GetSpaceHomeLeaveConfigQueryHandler : IRequestHandler<GetSpaceHomeLeaveConfigQuery, SpaceHomeLeaveConfigDto?>
{
    private readonly AppDbContext _db;

    public GetSpaceHomeLeaveConfigQueryHandler(AppDbContext db) => _db = db;

    public async Task<SpaceHomeLeaveConfigDto?> Handle(GetSpaceHomeLeaveConfigQuery request, CancellationToken ct)
    {
        var config = await _db.SpaceHomeLeaveConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.SpaceId == request.SpaceId, ct);

        if (config is null) return null;

        return new SpaceHomeLeaveConfigDto(
            config.Mode,
            config.BalanceValue,
            config.BaseDays,
            config.HomeDays,
            config.MinPeopleAtBase,
            config.MinRestHours,
            config.EligibilityThresholdHours,
            config.LeaveCapacity,
            config.LeaveDurationHours,
            config.EmergencyFreezeActive,
            config.EmergencyUseForScheduling,
            config.FreezeStartedAt,
            config.PreFreezeMode);
    }
}
