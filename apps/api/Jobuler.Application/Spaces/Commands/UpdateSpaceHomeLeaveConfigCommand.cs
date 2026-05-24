using Jobuler.Application.Common;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record UpdateSpaceHomeLeaveConfigCommand(
    Guid SpaceId,
    Guid UserId,
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
    bool EmergencyUseForScheduling) : IRequest;

public class UpdateSpaceHomeLeaveConfigCommandHandler : IRequestHandler<UpdateSpaceHomeLeaveConfigCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public UpdateSpaceHomeLeaveConfigCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task Handle(UpdateSpaceHomeLeaveConfigCommand request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(request.UserId, request.SpaceId, Permissions.OwnershipTransfer, ct);

        var config = await _db.SpaceHomeLeaveConfigs
            .FirstOrDefaultAsync(c => c.SpaceId == request.SpaceId, ct);

        if (config is null)
        {
            config = SpaceHomeLeaveConfig.Create(
                request.SpaceId,
                request.MinRestHours,
                request.EligibilityThresholdHours,
                request.LeaveCapacity,
                request.LeaveDurationHours,
                request.BalanceValue,
                request.Mode,
                request.BaseDays,
                request.HomeDays,
                request.MinPeopleAtBase);

            config.SetEmergencyFreezeActive(request.EmergencyFreezeActive);
            config.SetEmergencyUseForScheduling(request.EmergencyUseForScheduling);

            _db.SpaceHomeLeaveConfigs.Add(config);
        }
        else
        {
            config.SetMode(request.Mode);
            config.SetBalanceValue(request.BalanceValue);
            config.SetBaseDays(request.BaseDays);
            config.SetHomeDays(request.HomeDays);
            config.SetMinPeopleAtBase(request.MinPeopleAtBase);
            config.SetMinRestHours(request.MinRestHours);
            config.SetEligibilityThresholdHours(request.EligibilityThresholdHours);
            config.SetLeaveCapacity(request.LeaveCapacity);
            config.SetLeaveDurationHours(request.LeaveDurationHours);
            config.SetEmergencyFreezeActive(request.EmergencyFreezeActive);
            config.SetEmergencyUseForScheduling(request.EmergencyUseForScheduling);
        }

        await _db.SaveChangesAsync(ct);
    }
}
