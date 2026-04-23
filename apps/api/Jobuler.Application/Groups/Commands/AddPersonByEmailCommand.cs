using Jobuler.Domain.Groups;
using Jobuler.Domain.Notifications;
using Jobuler.Domain.People;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Jobuler.Application.Groups.Commands;

/// <summary>
/// Adds a person to a group by email.
/// - If a User with that email exists, links the Person to that User.
/// - If a Person already exists in this space with that email (via linked user), reuses it.
/// - Otherwise creates a new Person record.
/// - Sends a notification to the linked user (if any) with an opt-out token.
/// </summary>
public record AddPersonByEmailCommand(
    Guid SpaceId,
    Guid GroupId,
    string Email,
    Guid RequestingUserId) : IRequest<AddPersonByEmailResult>;

public record AddPersonByEmailResult(Guid PersonId, bool IsNewPerson, bool HasLinkedUser);

public class AddPersonByEmailCommandHandler : IRequestHandler<AddPersonByEmailCommand, AddPersonByEmailResult>
{
    private readonly AppDbContext _db;
    public AddPersonByEmailCommandHandler(AppDbContext db) => _db = db;

    public async Task<AddPersonByEmailResult> Handle(AddPersonByEmailCommand req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        // 1. Find user account by email
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        // 2. Find existing person in this space linked to that user
        Person? person = null;
        bool isNew = false;

        if (user is not null)
        {
            person = await _db.People.AsNoTracking()
                .FirstOrDefaultAsync(p => p.SpaceId == req.SpaceId && p.LinkedUserId == user.Id, ct);
        }

        // 3. Create person if not found
        if (person is null)
        {
            var displayName = user?.DisplayName ?? email.Split('@')[0];
            person = Person.Create(req.SpaceId, displayName, null, user?.Id);
            _db.People.Add(person);
            isNew = true;
        }

        await _db.SaveChangesAsync(ct); // ensure person.Id is set

        // 4. Add to group if not already a member
        var alreadyMember = await _db.GroupMemberships
            .AnyAsync(m => m.GroupId == req.GroupId && m.PersonId == person.Id, ct);

        if (!alreadyMember)
        {
            _db.GroupMemberships.Add(GroupMembership.Create(req.SpaceId, req.GroupId, person.Id));
        }

        // 5. Create invitation record with opt-out token
        var invitation = GroupInvitation.Create(req.SpaceId, req.GroupId, email, person.Id, req.RequestingUserId);
        _db.GroupInvitations.Add(invitation);

        // 6. Send notification if user has an account
        if (user is not null)
        {
            var group = await _db.Groups.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == req.GroupId, ct);
            var groupName = group?.Name ?? "קבוצה";

            var notification = Notification.Create(
                req.SpaceId, user.Id,
                "group_added",
                $"נוספת לקבוצה: {groupName}",
                $"הוספת לקבוצה \"{groupName}\". אם זו טעות, תוכל לעזוב את הקבוצה.",
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    groupId = req.GroupId,
                    optOutToken = invitation.OptOutToken
                }));
            _db.Notifications.Add(notification);
        }

        await _db.SaveChangesAsync(ct);
        return new AddPersonByEmailResult(person.Id, isNew, user is not null);
    }
}
