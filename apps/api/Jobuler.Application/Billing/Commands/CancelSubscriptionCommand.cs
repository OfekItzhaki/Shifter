using Jobuler.Application.Common;
using Jobuler.Domain.Billing;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Billing.Commands;

public record CancelSubscriptionCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid ActorUserId) : IRequest;

public class CancelSubscriptionCommandHandler : IRequestHandler<CancelSubscriptionCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogger _audit;

    public CancelSubscriptionCommandHandler(
        AppDbContext db,
        IPermissionService permissions,
        IAuditLogger audit)
    {
        _db = db;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task Handle(CancelSubscriptionCommand req, CancellationToken ct)
    {
        // ── Permission check ─────────────────────────────────────────────────
        await _permissions.RequirePermissionAsync(
            req.ActorUserId, req.SpaceId, Permissions.BillingManage, ct);

        // ── Load subscription ────────────────────────────────────────────────
        var sub = await _db.GroupSubscriptions
            .FirstOrDefaultAsync(s => s.GroupId == req.GroupId && s.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Subscription not found for group.");

        // ── Guard: already canceled or expired ───────────────────────────────
        if (sub.Status == SubscriptionStatus.Canceled || sub.Status == SubscriptionStatus.Expired)
            throw new InvalidOperationException("Subscription is already canceled.");

        // ── Trialing: cancel and immediately deactivate group ────────────────
        if (sub.Status == SubscriptionStatus.Trialing)
        {
            sub.Cancel();

            var group = await _db.Groups
                .FirstOrDefaultAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId, ct)
                ?? throw new KeyNotFoundException("Group not found.");

            group.Deactivate();
        }
        else
        {
            // ── Active/PastDue: cancel (expiry handled by background job) ────
            sub.Cancel();
        }

        await _db.SaveChangesAsync(ct);

        // ── Audit log ────────────────────────────────────────────────────────
        await _audit.LogAsync(
            req.SpaceId,
            req.ActorUserId,
            "subscription.cancel",
            entityType: "group_subscription",
            entityId: sub.Id,
            ct: ct);
    }
}
