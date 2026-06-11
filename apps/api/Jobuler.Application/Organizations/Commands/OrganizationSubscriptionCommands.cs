using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Organizations.Commands;

public record SetOrganizationSubscriptionCommand(
    Guid OrganizationId,
    OrganizationBillingMode BillingMode,
    string TierId,
    DateTime CurrentPeriodStart,
    DateTime? CurrentPeriodEnd,
    bool AutoRenew,
    string? ProviderSubscriptionId,
    string? ProviderCustomerId,
    int? CoveredSpaceLimit,
    int? CoveredMemberLimit) : IRequest;

public record CancelOrganizationSubscriptionCommand(Guid OrganizationId) : IRequest;

public class SetOrganizationSubscriptionCommandHandler : IRequestHandler<SetOrganizationSubscriptionCommand>
{
    private readonly AppDbContext _db;

    public SetOrganizationSubscriptionCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(SetOrganizationSubscriptionCommand request, CancellationToken ct)
    {
        var organizationExists = await _db.Organizations
            .AnyAsync(o => o.Id == request.OrganizationId, ct);
        if (!organizationExists)
            throw new KeyNotFoundException("Organization not found.");

        var subscription = await _db.OrganizationSubscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId, ct);

        if (subscription is null)
        {
            subscription = OrganizationSubscription.Create(
                request.OrganizationId,
                request.BillingMode,
                request.TierId,
                request.CurrentPeriodStart,
                request.CurrentPeriodEnd,
                request.AutoRenew,
                request.ProviderSubscriptionId,
                request.ProviderCustomerId,
                request.CoveredSpaceLimit,
                request.CoveredMemberLimit);
            _db.OrganizationSubscriptions.Add(subscription);
        }
        else
        {
            subscription.UpdateCoverage(
                request.BillingMode,
                request.TierId,
                request.CurrentPeriodStart,
                request.CurrentPeriodEnd,
                request.AutoRenew,
                request.ProviderSubscriptionId,
                request.ProviderCustomerId,
                request.CoveredSpaceLimit,
                request.CoveredMemberLimit);
        }

        await _db.SaveChangesAsync(ct);
    }
}

public class CancelOrganizationSubscriptionCommandHandler : IRequestHandler<CancelOrganizationSubscriptionCommand>
{
    private readonly AppDbContext _db;

    public CancelOrganizationSubscriptionCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(CancelOrganizationSubscriptionCommand request, CancellationToken ct)
    {
        var subscription = await _db.OrganizationSubscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == request.OrganizationId, ct)
            ?? throw new KeyNotFoundException("Organization subscription not found.");

        subscription.Cancel();
        await _db.SaveChangesAsync(ct);
    }
}
