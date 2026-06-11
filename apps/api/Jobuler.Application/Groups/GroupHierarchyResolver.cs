using Jobuler.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups;

public sealed record GroupHierarchyScope(
    Guid RootGroupId,
    IReadOnlySet<Guid> TreeGroupIds,
    IReadOnlySet<Guid> AncestorGroupIds);

public static class GroupHierarchyResolver
{
    public static async Task<GroupHierarchyScope?> ResolveAsync(
        AppDbContext db,
        Guid spaceId,
        Guid rootGroupId,
        CancellationToken ct)
    {
        var groups = await db.Groups.AsNoTracking()
            .Where(g => g.SpaceId == spaceId && g.DeletedAt == null && g.IsActive)
            .Select(g => new { g.Id, g.ParentGroupId })
            .ToListAsync(ct);

        if (!groups.Any(g => g.Id == rootGroupId))
            return null;

        var childrenByParent = groups
            .Where(g => g.ParentGroupId.HasValue)
            .GroupBy(g => g.ParentGroupId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

        var tree = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(rootGroupId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!tree.Add(current))
                continue;

            if (childrenByParent.TryGetValue(current, out var children))
            {
                foreach (var childId in children)
                    stack.Push(childId);
            }
        }

        var parentById = groups.ToDictionary(g => g.Id, g => g.ParentGroupId);
        var ancestors = new HashSet<Guid>();
        var visited = new HashSet<Guid> { rootGroupId };
        var cursor = parentById[rootGroupId];

        while (cursor.HasValue && parentById.ContainsKey(cursor.Value) && visited.Add(cursor.Value))
        {
            ancestors.Add(cursor.Value);
            cursor = parentById[cursor.Value];
        }

        return new GroupHierarchyScope(rootGroupId, tree, ancestors);
    }
}
