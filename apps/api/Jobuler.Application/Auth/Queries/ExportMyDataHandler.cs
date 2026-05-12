using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Auth.Queries;

public class ExportMyDataHandler : IRequestHandler<ExportMyDataQuery, UserDataExport>
{
    private readonly AppDbContext _db;
    public ExportMyDataHandler(AppDbContext db) => _db = db;

    public async Task<UserDataExport> Handle(ExportMyDataQuery request, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            ?? throw new KeyNotFoundException("User not found");

        // Profile
        var profile = new UserProfileData(
            user.Email,
            user.DisplayName,
            user.PhoneNumber,
            user.Birthday?.ToString("yyyy-MM-dd"),
            user.ProfileImageUrl,
            user.CreatedAt
        );

        // Find all Person records linked to this user
        var persons = await _db.People.AsNoTracking()
            .Where(p => p.LinkedUserId == request.UserId)
            .ToListAsync(ct);

        var personIds = persons.Select(p => p.Id).ToList();

        // Group memberships
        var groups = new List<UserGroupMembership>();
        if (personIds.Count > 0)
        {
            var memberships = await _db.GroupMemberships.AsNoTracking()
                .Where(gm => personIds.Contains(gm.PersonId))
                .ToListAsync(ct);

            var groupIds = memberships.Select(m => m.GroupId).Distinct().ToList();
            var groupEntities = await _db.Groups.AsNoTracking()
                .Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, ct);

            var spaceIds = groupEntities.Values.Select(g => g.SpaceId).Distinct().ToList();
            var spaceEntities = await _db.Spaces.AsNoTracking()
                .Where(s => spaceIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct);

            foreach (var membership in memberships)
            {
                var groupName = groupEntities.TryGetValue(membership.GroupId, out var g) ? g.Name : "Unknown";
                var spaceName = g != null && spaceEntities.TryGetValue(g.SpaceId, out var s) ? s.Name : "Unknown";

                groups.Add(new UserGroupMembership(
                    groupName,
                    spaceName,
                    membership.IsOwner ? "Owner" : "Member",
                    membership.JoinedAt
                ));
            }
        }

        // Assignments (last 90 days via TaskSlot dates)
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var assignments = new List<UserAssignment>();
        if (personIds.Count > 0)
        {
            var recentAssignments = await _db.Assignments.AsNoTracking()
                .Where(a => personIds.Contains(a.PersonId))
                .Take(500)
                .ToListAsync(ct);

            var taskSlotIds = recentAssignments.Select(a => a.TaskSlotId).Distinct().ToList();
            var taskSlots = await _db.TaskSlots.AsNoTracking()
                .Where(ts => taskSlotIds.Contains(ts.Id) && ts.StartsAt >= cutoff)
                .ToDictionaryAsync(ts => ts.Id, ct);

            var taskTypeIds = taskSlots.Values.Select(ts => ts.TaskTypeId).Distinct().ToList();
            var taskTypes = await _db.TaskTypes.AsNoTracking()
                .Where(tt => taskTypeIds.Contains(tt.Id))
                .ToDictionaryAsync(tt => tt.Id, ct);

            // Get group names via the person's group memberships
            var personGroupMap = await _db.GroupMemberships.AsNoTracking()
                .Where(gm => personIds.Contains(gm.PersonId))
                .ToListAsync(ct);

            var allGroupIds = personGroupMap.Select(gm => gm.GroupId).Distinct().ToList();
            var allGroups = await _db.Groups.AsNoTracking()
                .Where(g => allGroupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, ct);

            foreach (var assignment in recentAssignments)
            {
                if (!taskSlots.TryGetValue(assignment.TaskSlotId, out var slot))
                    continue; // slot is outside the 90-day window

                var taskName = taskTypes.TryGetValue(slot.TaskTypeId, out var tt) ? tt.Name : "Unknown";

                // Find the group name from the person's memberships in the same space
                var groupName = "Unknown";
                var personMemberships = personGroupMap.Where(gm => gm.PersonId == assignment.PersonId).ToList();
                foreach (var pm in personMemberships)
                {
                    if (allGroups.TryGetValue(pm.GroupId, out var grp) && grp.SpaceId == slot.SpaceId)
                    {
                        groupName = grp.Name;
                        break;
                    }
                }

                assignments.Add(new UserAssignment(
                    groupName,
                    taskName,
                    slot.StartsAt,
                    slot.EndsAt
                ));
            }

            assignments = assignments
                .OrderByDescending(a => a.StartsAt)
                .Take(500)
                .ToList();
        }

        // Notifications (last 30 days)
        var notifCutoff = DateTime.UtcNow.AddDays(-30);
        var notifications = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == request.UserId && n.CreatedAt >= notifCutoff)
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .Select(n => new UserNotification(
                n.Title,
                n.Body,
                n.EventType,
                n.CreatedAt,
                n.IsRead
            ))
            .ToListAsync(ct);

        return new UserDataExport(profile, groups, assignments, notifications);
    }
}
