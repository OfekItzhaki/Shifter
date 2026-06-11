using FluentValidation;
using Jobuler.Domain.Groups;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.SelfService.Commands;

public record UpdateSelfServiceConfigCommand(
    Guid SpaceId,
    Guid GroupId,
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
    bool AllowShiftSwaps) : IRequest<SelfServiceConfigDto>;

public record SelfServiceConfigDto(
    Guid Id,
    Guid GroupId,
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

public class UpdateSelfServiceConfigCommandValidator : AbstractValidator<UpdateSelfServiceConfigCommand>
{
    public UpdateSelfServiceConfigCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty();
        RuleFor(x => x.GroupId).NotEmpty();

        RuleFor(x => x.MinShiftsPerCycle)
            .InclusiveBetween(0, 100)
            .WithMessage("Min shifts per cycle must be between 0 and 100.");

        RuleFor(x => x.MaxShiftsPerCycle)
            .InclusiveBetween(1, 100)
            .WithMessage("Max shifts per cycle must be between 1 and 100.");

        RuleFor(x => x.MinShiftsPerCycle)
            .LessThanOrEqualTo(x => x.MaxShiftsPerCycle)
            .WithMessage("Min shifts per cycle must be less than or equal to max shifts per cycle.");

        RuleFor(x => x.RequestWindowOpenOffsetHours)
            .InclusiveBetween(1, 720)
            .WithMessage("Request window open offset must be between 1 and 720 hours.");

        RuleFor(x => x.RequestWindowCloseOffsetHours)
            .InclusiveBetween(1, 720)
            .WithMessage("Request window close offset must be between 1 and 720 hours.");

        RuleFor(x => x.RequestWindowOpenOffsetHours)
            .GreaterThan(x => x.RequestWindowCloseOffsetHours)
            .WithMessage("Request window open offset must be greater than close offset (open time must be before close time).");

        RuleFor(x => x.CancellationCutoffHours)
            .InclusiveBetween(1, 720)
            .WithMessage("Cancellation cutoff must be between 1 and 720 hours.");

        RuleFor(x => x.MaxAbsencesPerCycle)
            .InclusiveBetween(0, 100)
            .WithMessage("Max absence reports per cycle must be between 0 and 100.");

        RuleFor(x => x.MaxLateCancellationsPerCycle)
            .InclusiveBetween(0, 100)
            .WithMessage("Max late cancellations per cycle must be between 0 and 100.");

        RuleFor(x => x.LateCancellationWindowHours)
            .InclusiveBetween(1, 720)
            .WithMessage("Late cancellation window must be between 1 and 720 hours.");

        RuleFor(x => x.WaitlistOfferMinutes)
            .InclusiveBetween(15, 1440)
            .WithMessage("Waitlist offer duration must be between 15 and 1440 minutes.");

        RuleFor(x => x.CycleDurationDays)
            .InclusiveBetween(1, 30)
            .WithMessage("Cycle duration must be between 1 and 30 days.");
    }
}

public class UpdateSelfServiceConfigCommandHandler : IRequestHandler<UpdateSelfServiceConfigCommand, SelfServiceConfigDto>
{
    private readonly AppDbContext _db;

    public UpdateSelfServiceConfigCommandHandler(AppDbContext db) => _db = db;

    public async Task<SelfServiceConfigDto> Handle(UpdateSelfServiceConfigCommand req, CancellationToken ct)
    {
        var group = await _db.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct);

        if (group is null)
            throw new KeyNotFoundException("Group not found.");

        var config = await _db.SelfServiceConfigs
            .FirstOrDefaultAsync(c => c.GroupId == req.GroupId && c.SpaceId == req.SpaceId, ct);

        if (config is null)
        {
            config = SelfServiceConfig.Create(
                req.SpaceId,
                req.GroupId,
                req.MinShiftsPerCycle,
                req.MaxShiftsPerCycle,
                req.RequestWindowOpenOffsetHours,
                req.RequestWindowCloseOffsetHours,
                req.CancellationCutoffHours,
                req.MaxLateCancellationsPerCycle,
                req.LateCancellationWindowHours,
                req.WaitlistOfferMinutes,
                req.CycleDurationDays);

            config.SetWorkflowPermissions(
                req.AllowMemberShiftClaims,
                req.AllowWaitlist,
                req.AllowShiftChangeRequests,
                req.AllowAbsenceReports,
                req.AllowShiftSwaps);
            config.SetAbsenceReportLimit(req.MaxAbsencesPerCycle);

            _db.SelfServiceConfigs.Add(config);
        }
        else
        {
            // Requirement 5.7: Lowering Max_Shifts does not revoke existing approvals.
            // The domain entity's Update method applies the new values without touching
            // existing approved ShiftRequests. We simply persist the new config values.
            config.Update(
                req.MinShiftsPerCycle,
                req.MaxShiftsPerCycle,
                req.RequestWindowOpenOffsetHours,
                req.RequestWindowCloseOffsetHours,
                req.CancellationCutoffHours,
                req.MaxLateCancellationsPerCycle,
                req.LateCancellationWindowHours,
                req.WaitlistOfferMinutes,
                req.CycleDurationDays,
                req.AllowMemberShiftClaims,
                req.AllowWaitlist,
                req.AllowShiftChangeRequests,
                req.AllowAbsenceReports,
                req.AllowShiftSwaps,
                req.MaxAbsencesPerCycle);
        }

        await _db.SaveChangesAsync(ct);

        return new SelfServiceConfigDto(
            config.Id,
            config.GroupId,
            config.MinShiftsPerCycle,
            config.MaxShiftsPerCycle,
            config.RequestWindowOpenOffsetHours,
            config.RequestWindowCloseOffsetHours,
            config.CancellationCutoffHours,
            config.MaxAbsencesPerCycle,
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
