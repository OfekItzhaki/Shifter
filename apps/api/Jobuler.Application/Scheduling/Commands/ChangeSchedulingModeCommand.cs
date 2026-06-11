using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Scheduling;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jobuler.Application.Scheduling.Commands;

public record ChangeSchedulingModeCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId,
    SchedulingMode TargetMode) : IRequest;

public class ChangeSchedulingModeCommandValidator : AbstractValidator<ChangeSchedulingModeCommand>
{
    public ChangeSchedulingModeCommandValidator()
    {
        RuleFor(x => x.SpaceId).NotEmpty().WithMessage("SpaceId is required.");
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("GroupId is required.");
        RuleFor(x => x.RequestingUserId).NotEmpty().WithMessage("RequestingUserId is required.");
        RuleFor(x => x.TargetMode).IsInEnum().WithMessage("TargetMode must be a valid SchedulingMode value.");
    }
}

public class ChangeSchedulingModeCommandHandler : IRequestHandler<ChangeSchedulingModeCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly SelfServiceDefaultPolicyOptions _selfServiceDefaults;

    public ChangeSchedulingModeCommandHandler(AppDbContext db, IPermissionService permissions)
        : this(db, permissions, Options.Create(new SelfServiceDefaultPolicyOptions()))
    {
    }

    public ChangeSchedulingModeCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IOptions<SelfServiceDefaultPolicyOptions> selfServiceDefaults)
    {
        _db = db;
        _permissions = permissions;
        _selfServiceDefaults = selfServiceDefaults.Value;
    }

    public async Task Handle(ChangeSchedulingModeCommand request, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(
            request.RequestingUserId, request.SpaceId, Permissions.SchedulePublish, ct);

        var group = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == request.GroupId && g.SpaceId == request.SpaceId, ct);

        if (group is null)
            throw new KeyNotFoundException("Group not found.");

        group.EnsureActive();

        // No-op if already in the target mode
        if (group.SchedulingMode == request.TargetMode)
            return;

        // Requirement 1.5: Reject mode change if there are active Pending or Approved
        // shift requests for the current scheduling period.
        var hasActiveRequests = await _db.ShiftRequests
            .AnyAsync(sr =>
                sr.GroupId == request.GroupId &&
                sr.SpaceId == request.SpaceId &&
                (sr.Status == ShiftRequestStatus.Pending || sr.Status == ShiftRequestStatus.Approved), ct);

        if (hasActiveRequests)
            throw new InvalidOperationException(
                "Cannot change scheduling mode while shift requests with status Pending or Approved exist. " +
                "All unresolved requests must be cancelled or completed before switching modes.");

        group.SetSchedulingMode(request.TargetMode);

        if (request.TargetMode == SchedulingMode.SelfService)
        {
            var hasConfig = await _db.SelfServiceConfigs.AnyAsync(c =>
                c.GroupId == request.GroupId &&
                c.SpaceId == request.SpaceId, ct);

            if (!hasConfig)
                _db.SelfServiceConfigs.Add(_selfServiceDefaults.ToConfig(request.SpaceId, request.GroupId));
        }

        await _db.SaveChangesAsync(ct);
    }
}
