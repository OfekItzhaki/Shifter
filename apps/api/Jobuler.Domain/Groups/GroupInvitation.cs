using Jobuler.Domain.Common;

namespace Jobuler.Domain.Groups;

public class GroupInvitation : Entity, ITenantScoped
{
    public Guid SpaceId { get; private set; }
    public Guid GroupId { get; private set; }
    public string Email { get; private set; } = default!;
    public Guid? PersonId { get; private set; }
    public Guid? InvitedByUserId { get; private set; }
    public string OptOutToken { get; private set; } = default!;
    public string Status { get; private set; } = "active"; // active | opted_out
    public DateTime? OptedOutAt { get; private set; }

    private GroupInvitation() { }

    public static GroupInvitation Create(Guid spaceId, Guid groupId, string email,
        Guid? personId, Guid? invitedByUserId) =>
        new()
        {
            SpaceId = spaceId,
            GroupId = groupId,
            Email = email.Trim().ToLowerInvariant(),
            PersonId = personId,
            InvitedByUserId = invitedByUserId,
            OptOutToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")
        };

    public void OptOut() { Status = "opted_out"; OptedOutAt = DateTime.UtcNow; }
    public void LinkPerson(Guid personId) { PersonId = personId; }
}
