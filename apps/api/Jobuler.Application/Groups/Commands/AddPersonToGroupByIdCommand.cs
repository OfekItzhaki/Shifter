using Jobuler.Application.Common;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Notifications;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

/// <summary>
/// Adds an existing Person (already in the space) to a group by their PersonId.
/// Optionally assigns a group role at add time.
///
/// Role assignment rules:
/// - Group owner: may assign any active role that belongs to this group.
/// - Non-owner admin: may only add the member with no role (RoleId must be null).
///   The group owner can assign a role later via PATCH /members/{personId}/role.
/// </summary>
public record AddPersonToGroupByIdCommand(
    Guid SpaceId,
    Guid GroupId,
    Guid PersonId,
    Guid RequestingUserId,
    Guid? RoleId = null) : IRequest;

public class AddPersonToGroupByIdCommandHandler : IRequestHandler<AddPersonToGroupByIdCommand>
{
    private readonly AppDbContext _db;

    public AddPersonToGroupByIdCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(AddPersonToGroupByIdCommand req, CancellationToken ct)
    {
        // Verify person exists in this space
        var person = await _db.People
            .FirstOrDefaultAsync(p => p.Id == req.PersonId && p.SpaceId == req.SpaceId && p.IsActive, ct)
            ?? throw new KeyNotFoundException("Person not found in this space.");

        // Determine if the requesting user is the group owner
        var requestingPerson = await _db.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId && p.LinkedUserId == req.RequestingUserId, ct);

        var isGroupOwner = requestingPerson is not null && await _db.GroupMemberships.AsNoTracking()
            .AnyAsync(m => m.GroupId == req.GroupId && m.PersonId == requestingPerson.Id && m.IsOwner, ct);

        // Non-owners cannot assign roles at add time
        if (req.RoleId.HasValue && !isGroupOwner)
            throw new DomainValidationException(
                "Only the group owner can assign a role when adding a member. The member will be added with no role.");

        // Validate the role belongs to this group (if provided)
        if (req.RoleId.HasValue)
        {
            var roleExists = await _db.SpaceRoles.AsNoTracking()
                .AnyAsync(r => r.Id == req.RoleId.Value
                    && r.SpaceId == req.SpaceId
                    && r.GroupId == req.GroupId
                    && r.IsActive, ct);
            if (!roleExists)
                throw new KeyNotFoundException("Role not found in this group.");
        }

        // Idempotent — skip membership creation if already a member
        var alreadyMember = await _db.GroupMemberships
            .AnyAsync(m => m.GroupId == req.GroupId && m.PersonId == req.PersonId, ct);

        if (!alreadyMember)
        {
            // Check for duplicate name within the group (case-insensitive)
            var personName = person.FullName.Trim().ToLowerInvariant();
            var existingMemberIds = await _db.GroupMemberships.AsNoTracking()
                .Where(m => m.GroupId == req.GroupId && m.SpaceId == req.SpaceId)
                .Select(m => m.PersonId)
                .ToListAsync(ct);
            var duplicateNameInGroup = await _db.People.AsNoTracking()
                .AnyAsync(p => existingMemberIds.Contains(p.Id) && p.IsActive
                    && p.FullName.ToLower() == personName, ct);
            if (duplicateNameInGroup)
                throw new InvalidOperationException($"אדם בשם '{person.FullName}' כבר קיים בקבוצה זו.");

            // Check member limit based on subscription tier
            var sub = await _db.GroupSubscriptions
                .FirstOrDefaultAsync(s => s.GroupId == req.GroupId && s.SpaceId == req.SpaceId, ct);
            if (sub != null && sub.IsActive)
            {
                var currentCount = await _db.GroupMemberships.CountAsync(m => m.GroupId == req.GroupId, ct);
                var maxMembers = sub.TierId switch
                {
                    "starter" => 15,
                    "growth" => 30,
                    "team" => 60,
                    "org" => 90,
                    "unlimited" => int.MaxValue,
                    _ => int.MaxValue, // trial = no limit
                };
                if (currentCount >= maxMembers)
                    throw new InvalidOperationException($"MEMBER_LIMIT_REACHED:{maxMembers}");
            }

            _db.GroupMemberships.Add(GroupMembership.Create(req.SpaceId, req.GroupId, req.PersonId));

            // Notify linked user if they have an account
            if (person.LinkedUserId.HasValue)
            {
                var group = await _db.Groups.AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == req.GroupId, ct);
                var groupName = group?.Name ?? "קבוצה";

                _db.Notifications.Add(Notification.Create(
                    req.SpaceId, person.LinkedUserId.Value,
                    "group.member_added",
                    $"נוספת לקבוצה: {groupName}",
                    $"הוספת לקבוצה \"{groupName}\".",
                    System.Text.Json.JsonSerializer.Serialize(new { groupId = req.GroupId })));
            }
        }

        // Determine the effective role to assign:
        // - Owner provided a specific role → use it (already validated above)
        // - Non-owner → automatically assign the group's default role (no permissions)
        var effectiveRoleId = req.RoleId;
        if (!isGroupOwner)
        {
            effectiveRoleId = await _db.SpaceRoles.AsNoTracking()
                .Where(r => r.SpaceId == req.SpaceId && r.GroupId == req.GroupId && r.IsDefault && r.IsActive)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(ct);
        }

        // Assign role if we have one and the person isn't already assigned it
        if (effectiveRoleId.HasValue)
        {
            var alreadyAssigned = await _db.PersonRoleAssignments
                .AnyAsync(a => a.PersonId == req.PersonId
                    && a.RoleId == effectiveRoleId.Value
                    && a.GroupId == req.GroupId, ct);

            if (!alreadyAssigned)
                _db.PersonRoleAssignments.Add(
                    PersonRoleAssignment.Create(req.SpaceId, req.PersonId, effectiveRoleId.Value, req.GroupId));
        }

        await _db.SaveChangesAsync(ct);

        // Update peak member count for billing
        var memberCount = await _db.GroupMemberships.CountAsync(m => m.GroupId == req.GroupId, ct);
        var subForPeak = await _db.GroupSubscriptions.FirstOrDefaultAsync(s => s.GroupId == req.GroupId, ct);
        if (subForPeak != null)
        {
            subForPeak.UpdatePeakMemberCount(memberCount);
            await _db.SaveChangesAsync(ct);
        }
    }
}
