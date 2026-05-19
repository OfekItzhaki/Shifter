using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Commands;

public record RenewSubscriptionCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid ActorUserId) : IRequest;

public class RenewSubscriptionCommandHandler : IRequestHandler<RenewSubscriptionCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogger _audit;

    public RenewSubscriptionCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IAuditLogger audit)
    {
        _db = db;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task Handle(RenewSubscriptionCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.ActorUserId, req.SpaceId, Permissions.BillingManage, ct);

        var sub = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.GroupId == req.GroupId && s.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Subscription not found for group.");

        if (sub.Status == SubscriptionStatus.Active)
            throw new InvalidOperationException("Subscription is already active and does not need renewal.");

        if (sub.Status == SubscriptionStatus.Canceled && sub.CurrentPeriodEnd > DateTime.UtcNow)
        {
            // Within grace period — preserve existing billing period
            sub.Renew(sub.CurrentPeriodStart!.Value, sub.CurrentPeriodEnd!.Value);
        }
        else
        {
            // Expired or canceled past period end — create new billing period
            sub.Renew(DateTime.UtcNow, DateTime.UtcNow.AddMonths(1));
        }

        // Reactivate group if it was deactivated
        var group = await _db.Groups
            .FirstOrDefaultAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct);

        if (group is not null && !group.IsActive)
        {
            group.Reactivate();
        }

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(
            req.SpaceId,
            req.ActorUserId,
            "subscription.renew",
            entityType: "group_subscription",
            entityId: sub.Id,
            ct: ct);
    }
}
