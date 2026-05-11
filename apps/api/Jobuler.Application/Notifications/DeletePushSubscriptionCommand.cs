using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Notifications;

public record DeletePushSubscriptionCommand(
    Guid SpaceId, Guid UserId, string Endpoint) : IRequest;

public class DeletePushSubscriptionCommandHandler
    : IRequestHandler<DeletePushSubscriptionCommand>
{
    private readonly AppDbContext _db;
    public DeletePushSubscriptionCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeletePushSubscriptionCommand req, CancellationToken ct)
    {
        var subscription = await _db.PushSubscriptions.FirstOrDefaultAsync(
            s => s.UserId == req.UserId
              && s.SpaceId == req.SpaceId
              && s.Endpoint == req.Endpoint, ct);

        if (subscription is null) return; // idempotent — success even if not found

        _db.PushSubscriptions.Remove(subscription);
        await _db.SaveChangesAsync(ct);
    }
}
