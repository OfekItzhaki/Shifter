using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Spaces.Commands;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jobuler.Application.Spaces.Queries;

public record GetSpaceSelfServiceDefaultsQuery(Guid SpaceId) : IRequest<SpaceSelfServiceDefaultsDto>;

public class GetSpaceSelfServiceDefaultsQueryHandler
    : IRequestHandler<GetSpaceSelfServiceDefaultsQuery, SpaceSelfServiceDefaultsDto>
{
    private readonly AppDbContext _db;
    private readonly SelfServiceDefaultPolicyOptions _installDefaults;

    public GetSpaceSelfServiceDefaultsQueryHandler(
        AppDbContext db,
        IOptions<SelfServiceDefaultPolicyOptions> installDefaults)
    {
        _db = db;
        _installDefaults = installDefaults.Value;
    }

    public async Task<SpaceSelfServiceDefaultsDto> Handle(GetSpaceSelfServiceDefaultsQuery request, CancellationToken ct)
    {
        var defaults = await _db.SpaceSelfServiceDefaults
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SpaceId == request.SpaceId, ct);

        if (defaults is not null)
            return UpdateSpaceSelfServiceDefaultsCommandHandler.ToDto(defaults, "space");

        return new SpaceSelfServiceDefaultsDto(
            null,
            "install",
            _installDefaults.MinShiftsPerCycle,
            _installDefaults.MaxShiftsPerCycle,
            _installDefaults.RequestWindowOpenOffsetHours,
            _installDefaults.RequestWindowCloseOffsetHours,
            _installDefaults.CancellationCutoffHours,
            _installDefaults.MaxAbsencesPerCycle,
            _installDefaults.MaxLateCancellationsPerCycle,
            _installDefaults.LateCancellationWindowHours,
            _installDefaults.WaitlistOfferMinutes,
            _installDefaults.CycleDurationDays,
            _installDefaults.AllowMemberShiftClaims,
            _installDefaults.AllowWaitlist,
            _installDefaults.AllowShiftChangeRequests,
            _installDefaults.AllowAbsenceReports,
            _installDefaults.AllowShiftSwaps);
    }
}
