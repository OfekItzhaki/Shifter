using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record UpdateSpaceSelfServiceDefaultsCommand(
    Guid SpaceId,
    Guid UserId,
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

public record SpaceSelfServiceDefaultsDto(
    Guid? Id,
    string Source,
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
    bool AllowShiftSwaps);

public class UpdateSpaceSelfServiceDefaultsCommandValidator : AbstractValidator<UpdateSpaceSelfServiceDefaultsCommand>
{
    public UpdateSpaceSelfServiceDefaultsCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
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

public class UpdateSpaceSelfServiceDefaultsCommandHandler
    : IRequestHandler<UpdateSpaceSelfServiceDefaultsCommand, SpaceSelfServiceDefaultsDto>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public UpdateSpaceSelfServiceDefaultsCommandHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<SpaceSelfServiceDefaultsDto> Handle(UpdateSpaceSelfServiceDefaultsCommand request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(request.UserId, request.SpaceId, Permissions.OwnershipTransfer, ct);

        var exists = await _db.Spaces
            .AsNoTracking()
            .AnyAsync(s => s.Id == request.SpaceId && s.IsActive, ct);

        if (!exists)
            throw new KeyNotFoundException("Space not found.");

        var defaults = await _db.SpaceSelfServiceDefaults
            .FirstOrDefaultAsync(d => d.SpaceId == request.SpaceId, ct);

        if (defaults is null)
        {
            defaults = SpaceSelfServiceDefaults.Create(
                request.SpaceId,
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
            _db.SpaceSelfServiceDefaults.Add(defaults);
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
        return ToDto(defaults, "space");
    }

    public static SpaceSelfServiceDefaultsDto ToDto(SpaceSelfServiceDefaults defaults, string source) =>
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
}
