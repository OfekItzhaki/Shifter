using Jobuler.Domain.Notifications;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Notifications;

public record CreatePushSubscriptionCommand(
    Guid SpaceId,
    Guid UserId,
    string Endpoint,
    string P256dh,
    string Auth) : IRequest;

public class CreatePushSubscriptionCommandHandler
    : IRequestHandler<CreatePushSubscriptionCommand>
{
    private readonly AppDbContext _db;
    public CreatePushSubscriptionCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(CreatePushSubscriptionCommand req, CancellationToken ct)
    {
        // Check for existing subscription (upsert / idempotent)
        var existing = await _db.PushSubscriptions
            .FirstOrDefaultAsync(s =>
                s.UserId == req.UserId &&
                s.SpaceId == req.SpaceId &&
                s.Endpoint == req.Endpoint, ct);

        if (existing is not null)
            return; // Already subscribed — idempotent success

        // Domain entity Create method validates input
        // (HTTPS endpoint, non-empty p256dh/auth)
        var subscription = PushSubscription.Create(
            req.SpaceId, req.UserId, req.Endpoint, req.P256dh, req.Auth);

        _db.PushSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);
    }
}
