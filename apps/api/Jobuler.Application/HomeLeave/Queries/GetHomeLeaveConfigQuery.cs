using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.HomeLeave.Queries;

public record HomeLeaveConfigDto(
    Guid Id,
    Guid GroupId,
    Guid SpaceId,
    string Mode,
    int BaseDays,
    int HomeDays,
    decimal MinRestHours,
    decimal EligibilityThresholdHours,
    int LeaveCapacity,
    decimal LeaveDurationHours,
    int BalanceValue,
    int MinPeopleAtBase,
    bool EmergencyFreezeActive,
    bool EmergencyUseForScheduling,
    DateTime? FreezeStartedAt);

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
                Id: Guid.Empty,
                GroupId: req.GroupId,
                SpaceId: req.SpaceId,
                Mode: "automatic",
                BaseDays: 7,
                HomeDays: 2,
                MinRestHours: 0,
                EligibilityThresholdHours: 168,
                LeaveCapacity: 1,
                LeaveDurationHours: 48,
                BalanceValue: 50,
                MinPeopleAtBase: 8,
                EmergencyFreezeActive: false,
                EmergencyUseForScheduling: false,
                FreezeStartedAt: null);
        }

        return new HomeLeaveConfigDto(
            Id: config.Id,
            GroupId: config.GroupId,
            SpaceId: config.SpaceId,
            Mode: config.Mode.ToString().ToLowerInvariant(),
            BaseDays: config.BaseDays,
            HomeDays: config.HomeDays,
            MinRestHours: config.MinRestHours,
            EligibilityThresholdHours: config.EligibilityThresholdHours,
            LeaveCapacity: config.LeaveCapacity,
            LeaveDurationHours: config.LeaveDurationHours,
            BalanceValue: config.BalanceValue,
            MinPeopleAtBase: config.MinPeopleAtBase,
            EmergencyFreezeActive: config.EmergencyFreezeActive,
            EmergencyUseForScheduling: config.EmergencyUseForScheduling,
            FreezeStartedAt: config.FreezeStartedAt);
    }
}
