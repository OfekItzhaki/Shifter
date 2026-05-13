using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record DeactivateUnavailabilityReasonCommand(
    Guid SpaceId, Guid ReasonId, Guid RequestingUserId) : IRequest;

public class DeactivateUnavailabilityReasonCommandHandler
    : IRequestHandler<DeactivateUnavailabilityReasonCommand>
{
    private readonly AppDbContext _db;

    public DeactivateUnavailabilityReasonCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeactivateUnavailabilityReasonCommand req, CancellationToken ct)
    {
        var reason = await _db.UnavailabilityReasons
            .FirstOrDefaultAsync(r => r.Id == req.ReasonId && r.SpaceId == req.SpaceId && r.IsActive, ct);

        if (reason is null)
            throw new KeyNotFoundException($"Unavailability reason '{req.ReasonId}' not found in space.");

        reason.Deactivate();
        await _db.SaveChangesAsync(ct);
    }
}
