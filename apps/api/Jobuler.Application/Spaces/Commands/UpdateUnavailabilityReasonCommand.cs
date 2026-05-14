using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record UpdateUnavailabilityReasonCommand(
    Guid SpaceId, Guid ReasonId, string DisplayName, int SortOrder,
    Guid RequestingUserId) : IRequest;

public class UpdateUnavailabilityReasonCommandHandler
    : IRequestHandler<UpdateUnavailabilityReasonCommand>
{
    private readonly AppDbContext _db;

    public UpdateUnavailabilityReasonCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateUnavailabilityReasonCommand req, CancellationToken ct)
    {
        var reason = await _db.UnavailabilityReasons
            .FirstOrDefaultAsync(r => r.Id == req.ReasonId && r.SpaceId == req.SpaceId && r.IsActive, ct);

        if (reason is null)
            throw new KeyNotFoundException($"Unavailability reason '{req.ReasonId}' not found in space.");

        reason.Update(req.DisplayName, req.SortOrder);
        await _db.SaveChangesAsync(ct);
    }
}
