using Jobuler.Application.Common;
using Jobuler.Application.Tasks.Commands;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Tasks.Queries;

public record GetGroupTasksQuery(
    Guid SpaceId,
    Guid GroupId,
    Guid RequestingUserId) : IRequest<List<GroupTaskDto>>;

public class GetGroupTasksQueryHandler : IRequestHandler<GetGroupTasksQuery, List<GroupTaskDto>>
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public GetGroupTasksQueryHandler(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<List<GroupTaskDto>> Handle(GetGroupTasksQuery req, CancellationToken ct)
    {
        await _permissions.RequirePermissionAsync(req.RequestingUserId, req.SpaceId, Permissions.SpaceView, ct);

        // Verify group belongs to space
        var groupExists = await _db.Groups
            .AnyAsync(g => g.Id == req.GroupId && g.SpaceId == req.SpaceId && g.DeletedAt == null, ct);
        if (!groupExists)
            throw new KeyNotFoundException("Group not found in this space.");

        var tasks = await _db.GroupTasks.AsNoTracking()
            .Where(t => t.GroupId == req.GroupId && t.SpaceId == req.SpaceId && t.IsActive)
            .OrderBy(t => t.StartsAt)
            .ToListAsync(ct);

        return tasks.Select(t => new GroupTaskDto(
            t.Id,
            t.Name,
            t.StartsAt,
            t.EndsAt,
            t.ShiftDurationMinutes,
            t.RequiredHeadcount,
            t.BurdenLevel.ToString().ToLowerInvariant(),
            t.AllowsDoubleShift,
            t.AllowsOverlap,
            t.CreatedAt,
            t.UpdatedAt)).ToList();
    }
}
