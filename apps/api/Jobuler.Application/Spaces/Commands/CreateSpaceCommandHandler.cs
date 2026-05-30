using Jobuler.Application.Billing;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public class CreateSpaceCommandHandler : IRequestHandler<CreateSpaceCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITrialDurationCache _trialDurationCache;
    private readonly IStatisticsPeriodService _statisticsPeriodService;

    public CreateSpaceCommandHandler(
        AppDbContext db,
        ITrialDurationCache trialDurationCache,
        IStatisticsPeriodService statisticsPeriodService)
    {
        _db = db;
        _trialDurationCache = trialDurationCache;
        _statisticsPeriodService = statisticsPeriodService;
    }

    public async Task<Guid> Handle(CreateSpaceCommand request, CancellationToken ct)
    {
        var space = Space.Create(request.Name, request.RequestingUserId, request.Description, request.Locale);
        _db.Spaces.Add(space);

        // Owner automatically gets full membership with SpaceOwner permission level
        var membership = SpaceMembership.Create(space.Id, request.RequestingUserId);
        membership.SetPermissionLevel(SpacePermissionLevel.SpaceOwner);
        _db.SpaceMemberships.Add(membership);

        foreach (var perm in AllPermissions())
        {
            _db.SpacePermissionGrants.Add(
                SpacePermissionGrant.Grant(space.Id, request.RequestingUserId, perm, request.RequestingUserId));
        }

        // Save the space first so the FK exists for the subscription
        await _db.SaveChangesAsync(ct);

        // ── Auto-create trial subscription (idempotent) ──────────────────────
        var existingSubscription = await _db.SpaceSubscriptions
            .AnyAsync(s => s.SpaceId == space.Id, ct);

        if (!existingSubscription)
        {
            var trialDays = await _trialDurationCache.GetTrialDaysAsync(ct);
            var subscription = SpaceSubscription.CreateTrial(space.Id, trialDays);
            _db.SpaceSubscriptions.Add(subscription);
            await _db.SaveChangesAsync(ct);

            await _statisticsPeriodService.OnTrialStartedAsync(
                space.Id, subscription.TrialStartsAt, ct);
        }

        return space.Id;
    }

    private static IEnumerable<string> AllPermissions() =>
    [
        Permissions.SpaceView,
        Permissions.SpaceAdminMode,
        Permissions.PeopleManage,
        Permissions.ConstraintsManage,
        Permissions.RestrictionsManageSensitive,
        Permissions.TasksManage,
        Permissions.ScheduleRecalculate,
        Permissions.SchedulePublish,
        Permissions.ScheduleRollback,
        Permissions.PermissionsManage,
        Permissions.OwnershipTransfer,
        Permissions.LogsViewSensitive,
        Permissions.BillingManage,
    ];
}
