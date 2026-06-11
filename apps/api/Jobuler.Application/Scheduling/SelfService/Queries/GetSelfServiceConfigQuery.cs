using Jobuler.Application.Scheduling.SelfService.Commands;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService.Queries;

public record GetSelfServiceConfigQuery(Guid SpaceId, Guid GroupId) : IRequest<SelfServiceConfigDto?>;

public class GetSelfServiceConfigQueryHandler : IRequestHandler<GetSelfServiceConfigQuery, SelfServiceConfigDto?>
{
    private readonly AppDbContext _db;

    public GetSelfServiceConfigQueryHandler(AppDbContext db) => _db = db;

    public async Task<SelfServiceConfigDto?> Handle(GetSelfServiceConfigQuery req, CancellationToken ct)
    {
        var config = await _db.SelfServiceConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.GroupId == req.GroupId && c.SpaceId == req.SpaceId, ct);

        if (config is null)
            return null;

        return new SelfServiceConfigDto(
            config.Id,
            config.GroupId,
            config.MinShiftsPerCycle,
            config.MaxShiftsPerCycle,
            config.RequestWindowOpenOffsetHours,
            config.RequestWindowCloseOffsetHours,
            config.CancellationCutoffHours,
            config.MaxLateCancellationsPerCycle,
            config.LateCancellationWindowHours,
            config.WaitlistOfferMinutes,
            config.CycleDurationDays,
            config.AllowMemberShiftClaims,
            config.AllowWaitlist,
            config.AllowShiftChangeRequests,
            config.AllowAbsenceReports,
            config.AllowShiftSwaps);
    }
}
