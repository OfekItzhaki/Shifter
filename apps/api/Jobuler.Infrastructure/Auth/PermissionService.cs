using Jobuler.Application.Common;
using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Infrastructure.Auth;

public class PermissionService : IPermissionService
{
    private readonly AppDbContext _db;

    public PermissionService(AppDbContext db) => _db = db;

    public async Task<bool> HasPermissionAsync(Guid userId, Guid spaceId, string permissionKey, CancellationToken ct = default)
    {
        // Space owner always has all permissions
        var isOwner = await _db.Spaces
            .AnyAsync(s => s.Id == spaceId && s.OwnerUserId == userId && s.IsActive, ct);
        if (isOwner) return true;

        return await _db.SpacePermissionGrants
            .AnyAsync(g =>
                g.SpaceId == spaceId &&
                g.UserId == userId &&
                g.PermissionKey == permissionKey &&
                g.RevokedAt == null, ct);
    }

    public async Task RequirePermissionAsync(Guid userId, Guid spaceId, string permissionKey, CancellationToken ct = default)
    {
        if (!await HasPermissionAsync(userId, spaceId, permissionKey, ct))
            throw new UnauthorizedAccessException($"נדרשת הרשאה '{permissionKey}'.");
    }
}
