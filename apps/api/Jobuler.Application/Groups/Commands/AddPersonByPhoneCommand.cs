using Jobuler.Application.Billing;
using Jobuler.Application.Common;
using Jobuler.Domain.Groups;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.People;
using Jobuler.Domain.Spaces;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

/// <summary>
/// Adds a person to a group by phone number.
/// - If a User with that phone exists, links the Person to that User.
/// - If a Person already exists in this space with that phone, reuses it.
/// - Otherwise creates a new Person record with the phone number.
/// - Sends an in-app notification if the user has an account.
/// - Optionally assigns a group role at add time (group owner only).
/// </summary>
public record AddPersonByPhoneCommand(
    Guid SpaceId,
    Guid GroupId,
    string PhoneNumber,
    Guid RequestingUserId,
    Guid? RoleId = null) : IRequest<AddPersonByPhoneResult>;

public record AddPersonByPhoneResult(Guid PersonId, bool IsNewPerson, bool HasLinkedUser);

public class AddPersonByPhoneCommandHandler : IRequestHandler<AddPersonByPhoneCommand, AddPersonByPhoneResult>
{
    private readonly AppDbContext _db;
    private readonly IPeakMemberTracker _peakTracker;

    public AddPersonByPhoneCommandHandler(AppDbContext db, IPeakMemberTracker peakTracker)
    {
        _db = db;
        _peakTracker = peakTracker;
    }

    public async Task<AddPersonByPhoneResult> Handle(AddPersonByPhoneCommand req, CancellationToken ct)
    {
        var phone = req.PhoneNumber.Trim();

        // Determine if the requesting user is the group owner
        var requestingPerson = await _db.People.AsNoTracking()
            .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId && p.LinkedUserId == req.RequestingUserId, ct);

        var isGroupOwner = requestingPerson is not null && await _db.GroupMemberships.AsNoTracking()
            .AnyAsync(m => m.GroupId == req.GroupId && m.PersonId == requestingPerson.Id && m.IsOwner, ct);

        // Non-owners cannot assign roles at add time
        if (req.RoleId.HasValue && !isGroupOwner)
            throw new DomainValidationException(
                "Only the group owner can assign a role when adding a member.");

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

        // 1. Find user account by phone number
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PhoneNumber == phone, ct);

        // 2. Find existing person in this space with that phone or linked to that user
        Person? person = null;
        bool isNew = false;

        if (user is not null)
        {
            person = await _db.People
                .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId && p.LinkedUserId == user.Id, ct);
        }

        if (person is null)
        {
            person = await _db.People
                .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId && p.PhoneNumber == phone, ct);
        }

        // 3. Create person if not found
        if (person is null)
        {
            var displayName = user?.DisplayName ?? phone;
            person = Person.Create(req.SpaceId, displayName, null, user?.Id, phone);
            _db.People.Add(person);
            isNew = true;
        }

        await _db.SaveChangesAsync(ct);

        // 4. Add to group if not already a member
        var alreadyMember = await _db.GroupMemberships
            .AnyAsync(m => m.GroupId == req.GroupId && m.PersonId == person.Id, ct);

        if (!alreadyMember)
        {
            _db.GroupMemberships.Add(GroupMembership.Create(req.SpaceId, req.GroupId, person.Id));
        }

        // 5. Create invitation record (use phone as email field for tracking)
        var invitation = GroupInvitation.Create(req.SpaceId, req.GroupId, phone, person.Id, req.RequestingUserId);
        _db.GroupInvitations.Add(invitation);

        // 6. Send in-app notification if user has an account
        if (user is not null)
        {
            var group = await _db.Groups.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == req.GroupId, ct);
            var groupName = group?.Name ?? "Group";

            var notification = Notification.Create(
                req.SpaceId, user.Id,
                "group_added",
                $"Added to group: {groupName}",
                $"You were added to the group \"{groupName}\" via your phone number. If this was a mistake, you can leave the group.",
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    groupId = req.GroupId,
                    optOutToken = invitation.OptOutToken
                }));
            _db.Notifications.Add(notification);
        }

        // 7. Ensure SpaceMembership exists for linked user (idempotent)
        if (user is not null)
        {
            var hasSpaceMembership = await _db.SpaceMemberships.AnyAsync(
                sm => sm.UserId == user.Id && sm.SpaceId == req.SpaceId, ct);

            if (!hasSpaceMembership)
            {
                _db.SpaceMemberships.Add(SpaceMembership.Create(req.SpaceId, user.Id));
                _db.SpacePermissionGrants.Add(
                    SpacePermissionGrant.Grant(req.SpaceId, user.Id, Permissions.SpaceView, req.RequestingUserId));
            }
        }

        // 8. Assign role — owner uses provided roleId, non-owner gets the default role automatically
        var effectiveRoleId = req.RoleId;
        if (!isGroupOwner)
        {
            effectiveRoleId = await _db.SpaceRoles.AsNoTracking()
                .Where(r => r.SpaceId == req.SpaceId && r.GroupId == req.GroupId && r.IsDefault && r.IsActive)
                .Select(r => (Guid?)r.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (effectiveRoleId.HasValue)
        {
            var alreadyAssigned = await _db.PersonRoleAssignments
                .AnyAsync(a => a.PersonId == person.Id
                    && a.RoleId == effectiveRoleId.Value
                    && a.GroupId == req.GroupId, ct);

            if (!alreadyAssigned)
                _db.PersonRoleAssignments.Add(
                    PersonRoleAssignment.Create(req.SpaceId, person.Id, effectiveRoleId.Value, req.GroupId));
        }

        await _db.SaveChangesAsync(ct);

        // Track peak member count for space-level billing (only when a new person was created)
        if (isNew)
            await _peakTracker.TrackAsync(req.SpaceId, ct);

        return new AddPersonByPhoneResult(person.Id, isNew, user is not null);
    }
}
