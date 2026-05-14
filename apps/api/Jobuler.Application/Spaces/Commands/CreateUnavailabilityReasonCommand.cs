using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record CreateUnavailabilityReasonCommand(
    Guid SpaceId, string DisplayName, int SortOrder,
    Guid RequestingUserId) : IRequest<Guid>;

public class CreateUnavailabilityReasonCommandHandler
    : IRequestHandler<CreateUnavailabilityReasonCommand, Guid>
{
    private readonly AppDbContext _db;

    public CreateUnavailabilityReasonCommandHandler(AppDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateUnavailabilityReasonCommand req, CancellationToken ct)
    {
        var activeCount = await _db.UnavailabilityReasons.AsNoTracking()
            .CountAsync(r => r.SpaceId == req.SpaceId && r.IsActive, ct);

        if (activeCount >= 50)
            throw new InvalidOperationException("A space cannot have more than 50 active unavailability reasons.");

        var reason = UnavailabilityReason.Create(req.SpaceId, req.DisplayName, req.SortOrder);
        _db.UnavailabilityReasons.Add(reason);
        await _db.SaveChangesAsync(ct);
        return reason.Id;
    }
}
