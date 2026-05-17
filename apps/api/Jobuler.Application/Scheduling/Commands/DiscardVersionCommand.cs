using Jobuler.Application.Common;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Scheduling.Commands;

public record DiscardVersionCommand(
    Guid SpaceId,
    Guid VersionId,
    Guid RequestingUserId) : IRequest;

public class DiscardVersionCommandHandler : IRequestHandler<DiscardVersionCommand>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly ICacheService _cache;

    public DiscardVersionCommandHandler(AppDbContext db, IPermissionService permissions, ICacheService cache)
    {
        _db = db;
        _permissions = permissions;
        _cache = cache;
    }

    public async Task Handle(DiscardVersionCommand req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.SchedulePublish, ct);

        var version = await _db.ScheduleVersions
            .FirstOrDefaultAsync(v => v.Id == req.VersionId && v.SpaceId == req.SpaceId, ct)
            ?? throw new KeyNotFoundException("Schedule version not found.");

        version.Discard(); // throws InvalidOperationException if not Draft
        await _db.SaveChangesAsync(ct);

        // Invalidate cached schedule for all groups in this space
        await _cache.RemoveByPatternAsync($"schedule:{req.SpaceId}:*", ct);
    }
}
