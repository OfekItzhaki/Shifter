using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Spaces.Commands;

public record SeedUnavailabilityReasonsCommand(
    Guid SpaceId, List<string> ReasonDisplayNames,
    Guid RequestingUserId) : IRequest;

public class SeedUnavailabilityReasonsCommandHandler
    : IRequestHandler<SeedUnavailabilityReasonsCommand>
{
    private readonly AppDbContext _db;

    public SeedUnavailabilityReasonsCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(SeedUnavailabilityReasonsCommand req, CancellationToken ct)
    {
        var hasExistingReasons = await _db.UnavailabilityReasons.AsNoTracking()
            .AnyAsync(r => r.SpaceId == req.SpaceId && r.IsActive, ct);

        if (hasExistingReasons)
            return; // No-op: space already has configured reasons

        for (var i = 0; i < req.ReasonDisplayNames.Count; i++)
        {
            var reason = UnavailabilityReason.Create(req.SpaceId, req.ReasonDisplayNames[i], i);
            _db.UnavailabilityReasons.Add(reason);
        }

        await _db.SaveChangesAsync(ct);
    }
}
