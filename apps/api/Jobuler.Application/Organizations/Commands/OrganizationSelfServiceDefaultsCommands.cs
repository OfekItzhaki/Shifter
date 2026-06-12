using FluentValidation;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Application.Spaces.Commands;
using Jobuler.Domain.Organizations;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jobuler.Application.Organizations.Commands;

public record GetOrganizationSelfServiceDefaultsQuery(Guid OrganizationId)
    : IRequest<SpaceSelfServiceDefaultsDto>;

public record UpdateOrganizationSelfServiceDefaultsCommand(
    Guid OrganizationId,
    int MinShiftsPerCycle,
    int MaxShiftsPerCycle,
    int RequestWindowOpenOffsetHours,
    int RequestWindowCloseOffsetHours,
    int CancellationCutoffHours,
    int MaxAbsencesPerCycle,
    int MaxLateCancellationsPerCycle,
    int LateCancellationWindowHours,
    int WaitlistOfferMinutes,
    int CycleDurationDays,
    bool AllowMemberShiftClaims,
    bool AllowWaitlist,
    bool AllowShiftChangeRequests,
    bool AllowAbsenceReports,
    bool AllowShiftSwaps) : IRequest<SpaceSelfServiceDefaultsDto>;

public class UpdateOrganizationSelfServiceDefaultsCommandValidator
    : AbstractValidator<UpdateOrganizationSelfServiceDefaultsCommand>
{
    public UpdateOrganizationSelfServiceDefaultsCommandValidator()
    {
        RuleFor(x => x.OrganizationId).NotEmpty();
        RuleFor(x => x.MinShiftsPerCycle).InclusiveBetween(0, 100);
        RuleFor(x => x.MaxShiftsPerCycle).InclusiveBetween(1, 100);
        RuleFor(x => x.MinShiftsPerCycle).LessThanOrEqualTo(x => x.MaxShiftsPerCycle);
        RuleFor(x => x.RequestWindowOpenOffsetHours).InclusiveBetween(1, 720);
        RuleFor(x => x.RequestWindowCloseOffsetHours).InclusiveBetween(1, 720);
        RuleFor(x => x.RequestWindowOpenOffsetHours).GreaterThan(x => x.RequestWindowCloseOffsetHours);
        RuleFor(x => x.CancellationCutoffHours).InclusiveBetween(1, 720);
        RuleFor(x => x.MaxAbsencesPerCycle).InclusiveBetween(0, 100);
        RuleFor(x => x.MaxLateCancellationsPerCycle).InclusiveBetween(0, 100);
        RuleFor(x => x.LateCancellationWindowHours).InclusiveBetween(1, 720);
        RuleFor(x => x.WaitlistOfferMinutes).InclusiveBetween(15, 1440);
        RuleFor(x => x.CycleDurationDays).InclusiveBetween(1, 30);
    }
}

public class GetOrganizationSelfServiceDefaultsQueryHandler
    : IRequestHandler<GetOrganizationSelfServiceDefaultsQuery, SpaceSelfServiceDefaultsDto>
{
    private readonly AppDbContext _db;
    private readonly SelfServiceDefaultPolicyOptions _installDefaults;

    public GetOrganizationSelfServiceDefaultsQueryHandler(
        AppDbContext db,
        IOptions<SelfServiceDefaultPolicyOptions> installDefaults)
    {
        _db = db;
        _installDefaults = installDefaults.Value;
    }

    public async Task<SpaceSelfServiceDefaultsDto> Handle(
        GetOrganizationSelfServiceDefaultsQuery request,
        CancellationToken ct)
    {
        var organizationExists = await _db.Organizations
            .AsNoTracking()
            .AnyAsync(o => o.Id == request.OrganizationId, ct);

        if (!organizationExists)
            throw new KeyNotFoundException("Organization not found.");

        var defaults = await _db.OrganizationSelfServiceDefaults
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.OrganizationId == request.OrganizationId, ct);

        return defaults is null
            ? OrganizationSelfServiceDefaultsMapper.ToInstallDto(_installDefaults)
            : OrganizationSelfServiceDefaultsMapper.ToDto(defaults, "organization");
    }
}

public class UpdateOrganizationSelfServiceDefaultsCommandHandler
    : IRequestHandler<UpdateOrganizationSelfServiceDefaultsCommand, SpaceSelfServiceDefaultsDto>
{
    private readonly AppDbContext _db;

    public UpdateOrganizationSelfServiceDefaultsCommandHandler(AppDbContext db) => _db = db;

    public async Task<SpaceSelfServiceDefaultsDto> Handle(
        UpdateOrganizationSelfServiceDefaultsCommand request,
        CancellationToken ct)
    {
        var organizationExists = await _db.Organizations
            .AsNoTracking()
            .AnyAsync(o => o.Id == request.OrganizationId, ct);

        if (!organizationExists)
            throw new KeyNotFoundException("Organization not found.");

        var defaults = await _db.OrganizationSelfServiceDefaults
            .FirstOrDefaultAsync(d => d.OrganizationId == request.OrganizationId, ct);

        if (defaults is null)
        {
            defaults = OrganizationSelfServiceDefaults.Create(
                request.OrganizationId,
                request.MinShiftsPerCycle,
                request.MaxShiftsPerCycle,
                request.RequestWindowOpenOffsetHours,
                request.RequestWindowCloseOffsetHours,
                request.CancellationCutoffHours,
                request.MaxAbsencesPerCycle,
                request.MaxLateCancellationsPerCycle,
                request.LateCancellationWindowHours,
                request.WaitlistOfferMinutes,
                request.CycleDurationDays,
                request.AllowMemberShiftClaims,
                request.AllowWaitlist,
                request.AllowShiftChangeRequests,
                request.AllowAbsenceReports,
                request.AllowShiftSwaps);
            _db.OrganizationSelfServiceDefaults.Add(defaults);
        }
        else
        {
            defaults.Update(
                request.MinShiftsPerCycle,
                request.MaxShiftsPerCycle,
                request.RequestWindowOpenOffsetHours,
                request.RequestWindowCloseOffsetHours,
                request.CancellationCutoffHours,
                request.MaxAbsencesPerCycle,
                request.MaxLateCancellationsPerCycle,
                request.LateCancellationWindowHours,
                request.WaitlistOfferMinutes,
                request.CycleDurationDays,
                request.AllowMemberShiftClaims,
                request.AllowWaitlist,
                request.AllowShiftChangeRequests,
                request.AllowAbsenceReports,
                request.AllowShiftSwaps);
        }

        await _db.SaveChangesAsync(ct);
        return OrganizationSelfServiceDefaultsMapper.ToDto(defaults, "organization");
    }
}

public static class OrganizationSelfServiceDefaultsMapper
{
    public static SpaceSelfServiceDefaultsDto ToDto(
        OrganizationSelfServiceDefaults defaults,
        string source) =>
        new(
            defaults.Id,
            source,
            defaults.MinShiftsPerCycle,
            defaults.MaxShiftsPerCycle,
            defaults.RequestWindowOpenOffsetHours,
            defaults.RequestWindowCloseOffsetHours,
            defaults.CancellationCutoffHours,
            defaults.MaxAbsencesPerCycle,
            defaults.MaxLateCancellationsPerCycle,
            defaults.LateCancellationWindowHours,
            defaults.WaitlistOfferMinutes,
            defaults.CycleDurationDays,
            defaults.AllowMemberShiftClaims,
            defaults.AllowWaitlist,
            defaults.AllowShiftChangeRequests,
            defaults.AllowAbsenceReports,
            defaults.AllowShiftSwaps);

    public static SpaceSelfServiceDefaultsDto ToInstallDto(SelfServiceDefaultPolicyOptions defaults) =>
        new(
            null,
            "install",
            defaults.MinShiftsPerCycle,
            defaults.MaxShiftsPerCycle,
            defaults.RequestWindowOpenOffsetHours,
            defaults.RequestWindowCloseOffsetHours,
            defaults.CancellationCutoffHours,
            defaults.MaxAbsencesPerCycle,
            defaults.MaxLateCancellationsPerCycle,
            defaults.LateCancellationWindowHours,
            defaults.WaitlistOfferMinutes,
            defaults.CycleDurationDays,
            defaults.AllowMemberShiftClaims,
            defaults.AllowWaitlist,
            defaults.AllowShiftChangeRequests,
            defaults.AllowAbsenceReports,
            defaults.AllowShiftSwaps);
}
